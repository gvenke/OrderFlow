using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace OrderFlow.SharedInfrastructure;

public static class TestMessaging
{
    public const string Schema = "test";
    public const string VirtualHost = "orderflow-test";

    public static string? GetSchema(IConfiguration? configuration)
    {
        var testMode = configuration?.GetValue<bool>("Testing:Enabled") == true;
        var testVirtualHost = configuration?["RabbitMq:VirtualHost"] == VirtualHost;

        // Require both settings so the test broker cannot be paired with regular messaging tables.
        if (testMode != testVirtualHost)
            throw new InvalidOperationException(
                $"Testing:Enabled and RabbitMq:VirtualHost '{VirtualHost}' must be configured together.");

        return testMode ? Schema : null;
    }
}

public interface IMessagingSchemaContext
{
    string? MessagingSchema { get; }
}

public sealed class MessagingSchemaModelCacheKeyFactory : IModelCacheKeyFactory
{
    // EF normally caches one model per context type. Each schema needs its own mapping.
    public object Create(DbContext context, bool designTime) =>
        (context.GetType(), (context as IMessagingSchemaContext)?.MessagingSchema, designTime);
}
