using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace OrderFlow.IntegrationTests;

internal sealed class IntegrationTestResources : IDisposable
{
    private const string VirtualHost = "orderflow-test";
    private FileStream? _runLock;
    private readonly HttpClient _management;
    private readonly string _brokerUser;
    private readonly string _brokerPassword;
    private readonly string _brokerHost;

    public IntegrationTestResources(IConfiguration settings)
    {
        if (settings["RabbitMq:VirtualHost"] != VirtualHost)
            throw new InvalidOperationException($"Set RabbitMq:VirtualHost to '{VirtualHost}' in the test configuration.");

        OrdersConnectionString = Required(settings, "ConnectionStrings:OrderFlow");
        InventoryConnectionString = Required(settings, "ConnectionStrings:Inventory");
        _brokerHost = settings["RabbitMq:Host"] ?? "localhost";
        _brokerUser = Required(settings, "RabbitMq:Username");
        _brokerPassword = Required(settings, "RabbitMq:Password");
        var managementUser = settings["RabbitMq:ManagementUsername"] ?? _brokerUser;
        var managementPassword = settings["RabbitMq:ManagementPassword"] ?? _brokerPassword;
        var managementUrl = settings["RabbitMq:ManagementUrl"]
            ?? new UriBuilder("http", _brokerHost, 15672).Uri.AbsoluteUri;
        _management = new HttpClient
        {
            BaseAddress = new Uri(managementUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(60)
        };
        _management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{managementUser}:{managementPassword}")));
    }

    public string OrdersConnectionString { get; }
    public string InventoryConnectionString { get; }

    public async Task PrepareAsync()
    {
        // Hold the lock through teardown to prevent overlapping runs on this machine.
        var lockPath = Path.Combine(Path.GetTempPath(), "orderflow-test-vhost.lock");
        _runLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        // Queue purges remove ready messages. Active consumers can still hold unacknowledged deliveries.
        var connections = await _management.GetFromJsonAsync<JsonElement>($"api/vhosts/{VirtualHost}/connections");
        if (connections.GetArrayLength() != 0)
            throw new InvalidOperationException($"Stop the services connected to '{VirtualHost}' before running the suite.");

        // Purge message contents while keeping queues, exchanges, and bindings in place.
        var queues = await _management.GetFromJsonAsync<JsonElement>($"api/queues/{VirtualHost}");
        foreach (var queue in queues.EnumerateArray())
        {
            var name = Uri.EscapeDataString(queue.GetProperty("name").GetString()!);
            using var response = await _management.DeleteAsync($"api/queues/{VirtualHost}/{name}/contents");
            response.EnsureSuccessStatusCode();
        }
    }

    public void Configure(ProcessStartInfo startInfo)
    {
        startInfo.Environment["ConnectionStrings__OrderFlow"] = OrdersConnectionString;
        startInfo.Environment["ConnectionStrings__Inventory"] = InventoryConnectionString;
        startInfo.Environment["RabbitMq__Host"] = _brokerHost;
        startInfo.Environment["RabbitMq__Username"] = _brokerUser;
        startInfo.Environment["RabbitMq__Password"] = _brokerPassword;
        startInfo.Environment["RabbitMq__VirtualHost"] = VirtualHost;
        // Each child service selects its EF schema when it starts.
        startInfo.Environment["Testing__Enabled"] = "true";
    }

    public void Dispose()
    {
        _management.Dispose();
        _runLock?.Dispose();
    }

    private static string Required(IConfiguration settings, string key) =>
        settings[key] ?? throw new InvalidOperationException($"Missing integration test setting '{key}'.");
}
