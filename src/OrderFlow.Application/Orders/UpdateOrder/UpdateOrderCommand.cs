namespace OrderFlow.Application.Orders.UpdateOrder;

public record UpdateOrderCommand(
    int Id,
    int Quantity);
