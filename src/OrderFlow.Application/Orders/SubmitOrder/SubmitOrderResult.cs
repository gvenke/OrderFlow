
using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Orders.SubmitOrder;

public record SubmitOrderResult(
    int Id,
    Guid CorrelationId,
    OrderStatus Status);
