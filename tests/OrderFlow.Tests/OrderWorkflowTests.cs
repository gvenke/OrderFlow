using System.Data;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OrderFlow.Contracts.Requests;
using OrderFlow.Contracts.Responses;

[assembly: DoNotParallelize]

namespace OrderFlow.Tests;

[TestClass]
[TestCategory("Integration")]
public sealed class OrderWorkflowTests
{
    private static readonly IConfiguration Settings = new ConfigurationBuilder()
        .AddUserSecrets<OrderWorkflowTests>()
        .Build();

    private static readonly HttpClient Api = new()
    {
        BaseAddress =  new Uri($"{Settings["TestSettings:OrderFlowApiUrl"].TrimEnd('/')}/api/v1/orders/"),
        Timeout = TimeSpan.FromSeconds(15)
    };

    [TestMethod]
    public async Task DraftCanBeUpdatedListedAndCancelled()
    {
        var productId = Guid.NewGuid();
        var draft = await CreateDraftAsync(productId, 1);
        using var update = await Api.PutAsJsonAsync(draft.Id.ToString(), new UpdateOrderRequest(2));
        Assert.AreEqual(HttpStatusCode.NoContent, update.StatusCode);
        var order = await GetOrderAsync(draft.Id);
        Assert.AreEqual(productId, order.ProductId);
        Assert.AreEqual(2, order.Quantity);
        var orders = await Api.GetFromJsonAsync<GetOrderResponse[]>("");
        Assert.IsNotNull(orders);
        Assert.IsNotNull(Array.Find(orders, item => item.Id == draft.Id));
        await CancelAsync(draft.Id);
        Assert.AreEqual("Cancelled", (await GetOrderAsync(draft.Id)).Status);
        using var submit = await Api.PutAsync($"{draft.Id}/submit", null);
        Assert.AreEqual(HttpStatusCode.Conflict, submit.StatusCode);
    }

    [TestMethod]
    public async Task InvalidCreateReturnsBadRequest()
    {
        using var response = await Api.PostAsJsonAsync("", new CreateOrderRequest(Guid.Empty, 0));
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task MissingProductFailsAndCannotBeSubmittedAgain()
    {
        var draft = await CreateDraftAsync(Guid.NewGuid(), 1);
        var submitted = await SubmitAsync(draft.Id);
        await WaitForAsync("Failed", async () => (await GetOrderAsync(draft.Id)).Status);
        await AssertRejectionAsync(submitted.CorrelationId, "The product is missing from Inventory.");
        using var repeat = await Api.PutAsync($"{draft.Id}/submit", null);
        Assert.AreEqual(HttpStatusCode.Conflict, repeat.StatusCode);
    }

    [TestMethod]
    public async Task InsufficientStockFailsWithoutReducingAvailableStock()
    {
        var productId = await SeedStockAsync(1);
        var draft = await CreateDraftAsync(productId, 2);
        var submitted = await SubmitAsync(draft.Id);
        await WaitForAsync("Failed", async () => (await GetOrderAsync(draft.Id)).Status);
        await AssertRejectionAsync(submitted.CorrelationId, "Insufficient available stock.");
        Assert.AreEqual(1, await AvailableStockAsync(productId));
    }

    [TestMethod]
    public async Task ReservationReducesStockAndCancellationReturnsItOnce()
    {
        var productId = await SeedStockAsync(3);
        var draft = await CreateDraftAsync(productId, 2);
        var submitted = await SubmitAsync(draft.Id);
        var id = submitted.CorrelationId;
        await WaitForAsync("AwaitingPayment", () => ReadAsync<string>(
            "OrderFlow", "SELECT CurrentState FROM OrderSagaStates WHERE CorrelationId = @id", id));
        await WaitForAsync("Processing", async () => (await GetOrderAsync(draft.Id)).Status);
        Assert.AreEqual("Reserved", await ReservationStatusAsync(id));
        Assert.AreEqual(1, await AvailableStockAsync(productId));

        using var update = await Api.PutAsJsonAsync(draft.Id.ToString(), new UpdateOrderRequest(1));
        Assert.AreEqual(HttpStatusCode.Conflict, update.StatusCode);
        await CancelAsync(draft.Id);
        await WaitForAsync("Cancelled", async () => (await GetOrderAsync(draft.Id)).Status);
        Assert.AreEqual("Released", await ReservationStatusAsync(id));
        Assert.AreEqual(3, await AvailableStockAsync(productId));

        await CancelAsync(draft.Id);
        Assert.AreEqual("Cancelled", (await GetOrderAsync(draft.Id)).Status);
        Assert.AreEqual(3, await AvailableStockAsync(productId));
    }

    private static async Task<CreateOrderResponse> CreateDraftAsync(Guid productId, int quantity)
    {
        using var response = await Api.PostAsJsonAsync("", new CreateOrderRequest(productId, quantity));
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode, await response.Content.ReadAsStringAsync());
        var draft = await response.Content.ReadFromJsonAsync<CreateOrderResponse>();
        Assert.IsNotNull(draft);
        Assert.AreEqual("Draft", draft.Status);
        return draft;
    }

    private static async Task<SubmitOrderResponse> SubmitAsync(int id)
    {
        using var response = await Api.PutAsync($"{id}/submit", null);
        Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode, await response.Content.ReadAsStringAsync());
        var submitted = await response.Content.ReadFromJsonAsync<SubmitOrderResponse>();
        Assert.IsNotNull(submitted);
        Assert.AreEqual(id, submitted.Id);
        Assert.AreEqual("Pending", submitted.Status);
        Assert.AreNotEqual(Guid.Empty, submitted.CorrelationId);
        return submitted;
    }

    private static async Task CancelAsync(int id)
    {
        using var response = await Api.DeleteAsync(id.ToString());
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<GetOrderResponse> GetOrderAsync(int id) =>
        await Api.GetFromJsonAsync<GetOrderResponse>(id.ToString())
            ?? throw new AssertFailedException($"Order {id} returned no response body.");

    private static async Task WaitForAsync(string expected, Func<Task<string?>> read)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        string? actual;
        do
        {
            actual = await read();
            if (actual == expected)
                return;
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        } while (elapsed.Elapsed < TimeSpan.FromSeconds(45));

        Assert.Fail($"Expected '{expected}' within 45 seconds. Last observed: '{actual ?? "<missing>"}'.");
    }

    private static async Task<Guid> SeedStockAsync(int quantity)
    {
        var id = Guid.NewGuid();
        await using var connection = await OpenAsync("Inventory");
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO StockItems (ProductId, AvailableQuantity) VALUES (@id, @quantity)";
        command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
        command.Parameters.Add("@quantity", SqlDbType.Int).Value = quantity;
        Assert.AreEqual(1, await command.ExecuteNonQueryAsync());
        return id;
    }

    private static async Task AssertRejectionAsync(Guid correlationId, string reason)
    {
        Assert.AreEqual("Rejected", await ReservationStatusAsync(correlationId));
        Assert.AreEqual(reason, await ReadAsync<string>("Inventory",
            "SELECT RejectionReason FROM InventoryReservations WHERE CorrelationId = @id", correlationId));
    }

    private static Task<string?> ReservationStatusAsync(Guid correlationId) =>
        ReadAsync<string>("Inventory",
            "SELECT Status FROM InventoryReservations WHERE CorrelationId = @id", correlationId);

    private static Task<int> AvailableStockAsync(Guid productId) =>
        ReadAsync<int>("Inventory", "SELECT AvailableQuantity FROM StockItems WHERE ProductId = @id", productId);

    private static async Task<T?> ReadAsync<T>(string database, string sql, Guid id)
    {
        await using var connection = await OpenAsync(database);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
        var value = await command.ExecuteScalarAsync();
        if (value is T result)
            return result;
        if (typeof(T).IsValueType || value is not null and not DBNull)
            throw new AssertFailedException($"No {typeof(T).Name} value found for {id} in {database}.");
        return default;
    }

    private static async Task<SqlConnection> OpenAsync(string database)
    {
        var connectionString = Environment.GetEnvironmentVariable($"ConnectionStrings__{database}")
            ?? Settings.GetConnectionString(database);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new AssertFailedException($"Set ConnectionStrings:{database} in the shared OrderFlow user secrets.");

        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync();
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
