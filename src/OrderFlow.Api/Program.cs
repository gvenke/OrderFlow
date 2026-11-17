
using OrderFlow.Api.ExceptionHandling;
using OrderFlow.Application.Orders;
using OrderFlow.Application.Orders.CreateOrder;
using OrderFlow.Application.Orders.UpdateOrder;
using OrderFlow.Persistence;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Global exception handling
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions.TryAdd(
            "traceId",
            Activity.Current?.Id ??
            context.HttpContext.TraceIdentifier);
    };
});

builder.Services.AddOpenApi();
builder.Services.AddApplication();

builder.Services.AddInfrastructure(
    builder.Configuration);

var app = builder.Build();

// Exception handler should run early
app.UseExceptionHandler();

app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
