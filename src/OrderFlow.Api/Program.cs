
using OrderFlow.Application.Orders;
using OrderFlow.Application.Orders.CreateOrder;
using OrderFlow.Application.Orders.UpdateOrder;
using OrderFlow.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplication();

builder.Services.AddInfrastructure(
    builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
