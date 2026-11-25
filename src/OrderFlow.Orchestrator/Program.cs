
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Health;
using OrderFlow.Orchestrator.Persistence;
using OrderFlow.Orchestrator.Sagas;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------
// Configuration
// -----------------------------------------------------

var connectionString = builder.Configuration
    .GetConnectionString("OrderFlow")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:OrderFlow is missing.");

var rabbitHost = builder.Configuration["RabbitMq:Host"]
    ?? "localhost";

var rabbitUsername = builder.Configuration["RabbitMq:Username"]
    ?? throw new InvalidOperationException(
        "RabbitMq:Username is missing.");

var rabbitPassword = builder.Configuration["RabbitMq:Password"]
    ?? throw new InvalidOperationException(
        "RabbitMq:Password is missing.");

// -----------------------------------------------------
// EF Core - Saga Persistence
// -----------------------------------------------------

builder.Services.AddDbContext<OrderSagaDbContext>(options =>
{
    options.UseSqlServer(connectionString, sql =>
    {
        // Separate migration history from Orders API
        sql.MigrationsHistoryTable(
            "__OrderSagaMigrationsHistory");
    });
});

// -----------------------------------------------------
// MassTransit
// -----------------------------------------------------

builder.Services.AddMassTransit(x =>
{
    // Register our Order state machine
    x.AddSagaStateMachine<
        OrderStateMachine,
        OrderState,
        OrderStateDefinition>()
        .EntityFrameworkRepository(r =>
        {
            // SQL Server pessimistic concurrency
            r.ConcurrencyMode =
                ConcurrencyMode.Pessimistic;

            // Use our registered DbContext
            r.ExistingDbContext<OrderSagaDbContext>();
        });

    // Consumer Outbox for reliable message processing
    x.AddEntityFrameworkOutbox<OrderSagaDbContext>(o =>
    {
        o.UseSqlServer();
    });

    // RabbitMQ transport
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUsername);
            h.Password(rabbitPassword);
        });

        // Creates the receive endpoints for registered sagas
        cfg.ConfigureEndpoints(context);
    });
});

// NEW: Wait for the MassTransit bus to start.
builder.Services.Configure<MassTransitHostOptions>(
    options =>
    {
        options.WaitUntilStarted = true;
        options.StartTimeout = TimeSpan.FromSeconds(60);
    });

builder.Services.AddHostedService<InventoryDeadlineWorker>();

// NEW: Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrderSagaDbContext>(
        name: "saga-database",
        tags: new[] { "ready" });

// -----------------------------------------------------
// Start application
// -----------------------------------------------------

var app = builder.Build();

var logger = app.Services
    .GetRequiredService<ILogger<Program>>();

var lifetime = app.Services
    .GetRequiredService<IHostApplicationLifetime>();

lifetime.ApplicationStarted.Register(() =>
{
    logger.LogInformation(
        "OrderFlow Orchestrator has started successfully.");
});

lifetime.ApplicationStopping.Register(() =>
{
    logger.LogInformation(
        "OrderFlow Orchestrator is shutting down.");
});

// The Orchestrator doesn't need controllers.
app.MapOrderFlowHealthChecks();

app.Run();
