using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Health;
using OrderFlow.Inventory.Consumers;
using OrderFlow.Inventory.Infrastructure;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Inventory")
    ?? throw new InvalidOperationException("ConnectionStrings:Inventory is missing.");

var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";

var rabbitUsername = builder.Configuration["RabbitMq:Username"]
    ?? throw new InvalidOperationException("RabbitMq:Username is missing.");

var rabbitPassword = builder.Configuration["RabbitMq:Password"]
    ?? throw new InvalidOperationException("RabbitMq:Password is missing.");

builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<InventoryConsumer>();

    x.AddEntityFrameworkOutbox<InventoryDbContext>(options =>
    {
        options.UseSqlServer();
        options.IsolationLevel = IsolationLevel.Serializable;
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, "/", host =>
        {
            host.Username(rabbitUsername);
            host.Password(rabbitPassword);
        });

        cfg.ReceiveEndpoint("inventory", endpoint =>
        {
            endpoint.UseMessageRetry(retry =>
                retry.Intervals(100, 500, 1000, 2000));

            endpoint.UseEntityFrameworkOutbox<InventoryDbContext>(context);
            endpoint.ConfigureConsumer<InventoryConsumer>(context);
        });
    });
});

// NEW: Wait for MassTransit startup.
builder.Services.Configure<MassTransitHostOptions>(
    options =>
    {
        options.WaitUntilStarted = true;
        options.StartTimeout = TimeSpan.FromSeconds(60);
    });

// NEW: Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<InventoryDbContext>(
        name: "inventory-database",
        tags: new[] { "ready" });


var app = builder.Build();

app.MapOrderFlowHealthChecks();

app.Run();