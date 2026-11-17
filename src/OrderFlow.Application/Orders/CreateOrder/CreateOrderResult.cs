using OrderFlow.Domain.Orders;
using System;
using System.Collections.Generic;
using System.Text;

namespace OrderFlow.Application.Orders.CreateOrder;

public record CreateOrderResult(
    int Id,
    Guid CorrelationId,
    OrderStatus Status);