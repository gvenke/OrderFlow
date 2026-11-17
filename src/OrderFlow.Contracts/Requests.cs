using System;
using System.Collections.Generic;
using System.Text;

namespace OrderFlow.Contracts.Requests;

public record CreateOrderRequest(
    Guid ProductId,
    int Quantity);

public record UpdateOrderRequest(
    Guid ProductId,
    int Quantity);
