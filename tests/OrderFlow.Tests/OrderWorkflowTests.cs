using System.Data;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OrderFlow.Contracts.Requests;
using OrderFlow.Contracts.Responses;
using OrderFlow.IntegrationTests;

[assembly: DoNotParallelize]

namespace OrderFlow.Tests;

[TestClass]
[TestCategory("Integration")]
public sealed class OrderWorkflowTests
{
    private static readonly Dictionary<int, Guid> CreatedOrders = new();
    private static readonly HashSet<Guid> StockFixtures = new();
    private static readonly HttpClient Api = new()
    {
        BaseAddress = new Uri(IntegrationTestSetup.ApiAddress, "/api/v1/orders/"),
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
    public async Task ConcurrentOrdersForLastUnitProduceExactlyOneReservation()
    {
        var productId = await SeedStockAsync(1);
        var first = await CreateDraftAsync(productId, 1);
        var second = await CreateDraftAsync(productId, 1);

        var submitted = await Task.WhenAll(SubmitAsync(first.Id), SubmitAsync(second.Id));
        string?[] outcomes = [];
        await WaitForAsync("Recorded", async () =>
        {
            outcomes = await Task.WhenAll(
                ReservationStatusAsync(submitted[0].CorrelationId),
                ReservationStatusAsync(submitted[1].CorrelationId));
            return outcomes.All(status => status is not null)
                ? "Recorded"
                : string.Join(", ", outcomes.Select(status => status ?? "<missing>"));
        });

        CollectionAssert.AreEquivalent(new[] { "Reserved", "Rejected" }, outcomes);
        var winnerIndex = Array.IndexOf(outcomes, "Reserved");
        var winner = submitted[winnerIndex];
        var loser = submitted[1 - winnerIndex];

        await WaitForAsync("Processing", async () => (await GetOrderAsync(winner.Id)).Status);
        await WaitForAsync("Failed", async () => (await GetOrderAsync(loser.Id)).Status);
        await AssertRejectionAsync(loser.CorrelationId, "Insufficient available stock.");
        Assert.AreEqual(0, await AvailableStockAsync(productId));
    }

    [TestMethod]
    public async Task ReservationReducesStockAndCancellationReturnsItOnce()
    {
        var productId = await SeedStockAsync(3);
        var draft = await CreateDraftAsync(productId, 2);
        var submitted = await SubmitAsync(draft.Id);
        var id = submitted.CorrelationId;
        await WaitForAsync("AwaitingPayment", () => ReadAsync<string>(
            "OrderFlow", "SELECT CurrentState FROM test.OrderSagaStates WHERE CorrelationId = @id", id));
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

    [TestMethod]
    public async Task MissingReservationReplyRetriesWithoutDeductingStockAgain()
    {
        var productId = await SeedStockAsync(3);
        var draft = await CreateDraftAsync(productId, 2);
        var submitted = await SubmitAsync(draft.Id);
        var id = submitted.CorrelationId;
        Task<string?> ReadSagaState() => ReadAsync<string>(
            "OrderFlow", "SELECT CurrentState FROM test.OrderSagaStates WHERE CorrelationId = @id", id);

        await WaitForAsync("AwaitingPayment", ReadSagaState);
        await WaitForAsync("Processing", async () => (await GetOrderAsync(draft.Id)).Status);
        Assert.AreEqual(1, await AvailableStockAsync(productId));

        // Re-create the durable state left when Inventory committed but its reply never arrived.
        // The real worker, RabbitMQ, saga, and Inventory consumer handle the retry.
        await using (var connection = await OpenAsync("OrderFlow"))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE test.OrderSagaStates
                SET CurrentState = 'AwaitingInventory',
                    InventoryAttempts = 1,
                    InventoryDeadlineUtc = SYSUTCDATETIME()
                WHERE CorrelationId = @id AND CurrentState = 'AwaitingPayment'
                """;
            command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
            Assert.AreEqual(1, await command.ExecuteNonQueryAsync());
        }

        await WaitForAsync("AwaitingPayment", ReadSagaState);
        Assert.AreEqual(2, await ReadAsync<int>("OrderFlow",
            "SELECT InventoryAttempts FROM test.OrderSagaStates WHERE CorrelationId = @id", id));
        Assert.AreEqual("Reserved", await ReservationStatusAsync(id));
        Assert.AreEqual(1, await AvailableStockAsync(productId));

        await CancelAsync(draft.Id);
        await WaitForAsync("Cancelled", async () => (await GetOrderAsync(draft.Id)).Status);
        Assert.AreEqual(3, await AvailableStockAsync(productId));
    }

    private static async Task<CreateOrderResponse> CreateDraftAsync(Guid productId, int quantity)
    {
        using var response = await Api.PostAsJsonAsync("", new CreateOrderRequest(productId, quantity));
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode, await response.Content.ReadAsStringAsync());
        var draft = await response.Content.ReadFromJsonAsync<CreateOrderResponse>();
        Assert.IsNotNull(draft);
        CreatedOrders.Add(draft.Id, productId);
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
        StockFixtures.Add(id);
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

    internal static async Task CleanupTestRowsAsync()
    {
        var failures = new List<Exception>();
        var unresolvedProducts = new HashSet<Guid>();
        foreach (var (orderId, productId) in CreatedOrders)
        {
            try
            {
                await CleanupOrderAsync(orderId, productId);
            }
            catch (Exception exception)
            {
                unresolvedProducts.Add(productId);
                failures.Add(new InvalidOperationException($"Could not clean up test order {orderId}.", exception));
            }
        }

        foreach (var productId in StockFixtures.Except(unresolvedProducts))
        {
            try
            {
                await using var inventory = await OpenAsync("Inventory");
                await using var command = inventory.CreateCommand();
                command.CommandText = """
                    DELETE FROM StockItems WHERE ProductId = @productId
                    AND NOT EXISTS (SELECT 1 FROM InventoryReservations WHERE ProductId = @productId)
                    """;
                command.Parameters.Add("@productId", SqlDbType.UniqueIdentifier).Value = productId;
                if (await command.ExecuteNonQueryAsync() != 1)
                    throw new InvalidOperationException("The fixture still has reservations or is missing.");
            }
            catch (Exception exception)
            {
                failures.Add(new InvalidOperationException($"Could not clean up stock fixture {productId}.", exception));
            }
        }

        CreatedOrders.Clear();
        StockFixtures.Clear();
        if (failures.Count > 0)
            throw new AggregateException("Test data cleanup failed.", failures);
    }

    private static async Task CleanupOrderAsync(int orderId, Guid productId)
    {
        await using var orders = await OpenAsync("OrderFlow");
        await using var find = orders.CreateCommand();
        find.CommandText = "SELECT CorrelationId FROM Orders WHERE Id = @orderId AND ProductId = @productId";
        find.Parameters.Add("@orderId", SqlDbType.Int).Value = orderId;
        find.Parameters.Add("@productId", SqlDbType.UniqueIdentifier).Value = productId;
        if (await find.ExecuteScalarAsync() is not Guid correlationId)
            throw new InvalidOperationException("The tracked test order was not found in the configured database.");

        var status = (await GetOrderAsync(orderId)).Status;
        if (status is "Pending" or "Processing")
        {
            using var cancellation = await Api.DeleteAsync(orderId.ToString());
            if (cancellation.StatusCode != HttpStatusCode.Conflict)
                Assert.AreEqual(HttpStatusCode.NoContent, cancellation.StatusCode, await cancellation.Content.ReadAsStringAsync());
        }

        await WaitForAsync("Settled", async () =>
        {
            var current = (await GetOrderAsync(orderId)).Status;
            return current is "Draft" or "Failed" or "Cancelled" or "Completed" ? "Settled" : current;
        });

        await using var inventory = await OpenAsync("Inventory");
        await using var delete = inventory.CreateCommand();
        delete.CommandText = """
            DELETE FROM InventoryReservations
            WHERE CorrelationId = @id AND Status IN ('Rejected', 'Released')
            """;
        delete.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = correlationId;
        await delete.ExecuteNonQueryAsync();
        Assert.IsNull(await ReservationStatusAsync(correlationId), "Cleanup cannot remove a reservation that still holds stock.");

        await using var transaction = orders.BeginTransaction();
        await using var deleteOrder = orders.CreateCommand();
        deleteOrder.Transaction = transaction;
        deleteOrder.CommandText = """
            DELETE FROM test.OrderSagaStates WHERE CorrelationId = @id;
            DELETE FROM Orders WHERE Id = @orderId AND ProductId = @productId AND CorrelationId = @id;
            """;
        deleteOrder.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = correlationId;
        deleteOrder.Parameters.Add("@orderId", SqlDbType.Int).Value = orderId;
        deleteOrder.Parameters.Add("@productId", SqlDbType.UniqueIdentifier).Value = productId;
        await deleteOrder.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    private static async Task<SqlConnection> OpenAsync(string database)
    {
        var connectionString = IntegrationTestSetup.GetConnectionString(database);

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
