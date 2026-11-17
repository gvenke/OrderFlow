namespace OrderFlow.Application.Orders.UpdateOrder;

public record UpdateOrderCommand(
    int Id,
    Guid ProductId,
    int Quantity);
