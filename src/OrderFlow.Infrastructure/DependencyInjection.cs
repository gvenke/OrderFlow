
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Orders;
using OrderFlow.Application.Orders.CreateOrder;
using OrderFlow.Application.Orders.UpdateOrder;
using OrderFlow.Infrastructure.Consumers;
using OrderFlow.Persistence.Messaging;
using OrderFlow.Persistence.Repositories;

namespace OrderFlow.Persistence;

public static class DependencyInjection
{

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateOrderHandler>();
        services.AddScoped<CancelOrderHandler>();
        services.AddScoped<UpdateOrderHandler>();
        services.AddScoped<SubmitOrderHandler>();
        services.AddScoped<OrderService>();
        services.AddScoped<OrderEventService>();
        return services;
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration
            .GetConnectionString("OrderFlow")
            ?? throw new InvalidOperationException(
                "OrderFlow connection string is missing.");

        services.AddDbContext<OrderFlowDbContext>(options =>
        {
            options.UseSqlServer(connectionString);

        });

        services.AddScoped<IOrderRepository, OrderRepository>();

        services.AddScoped<IUnitOfWork, PersistenceUnitOfWork>();

        services.AddScoped<
            IIntegrationEventPublisher,
            MassTransitEventPublisher>();

        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<OrderFlowDbContext>(o =>
            {
                // Resolve SQL lock table names from the EF model for the current schema.
                o.UseSqlServer(false);
                o.UseBusOutbox();
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                var host = configuration["RabbitMq:Host"] ?? throw new InvalidOperationException("RabbitMq:Host is missing.");
                var username = configuration["RabbitMq:Username"] ?? throw new InvalidOperationException("RabbitMq:Username is missing.");
                var password = configuration["RabbitMq:Password"] ?? throw new InvalidOperationException("RabbitMq:Password is missing.");

                cfg.Host(host, configuration["RabbitMq:VirtualHost"] ?? "/", h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                cfg.ConfigureEndpoints(context);
            });

            x.AddConsumer<OrderStatusConsumer, OrderStatusConsumerDefinition>();
            //x.AddConsumer<OrderCancelledConsumer>();
        });

        // Allow startup to wait for MassTransit.
        // Only add this if not already configured.
        services.Configure<MassTransitHostOptions>(
            options =>
            {
                options.WaitUntilStarted = true;
                options.StartTimeout = TimeSpan.FromSeconds(60);
            });

        //  Health checks
        services.AddHealthChecks()
            .AddDbContextCheck<OrderFlowDbContext>(
                name: "orders-database",
                tags: new[] { "ready" });

        return services;
    }

}
