
namespace OrderFlow.Application.Orders.CreateOrder;

public record CreateOrderCommand(
    Guid ProductId,
    int Quantity);
