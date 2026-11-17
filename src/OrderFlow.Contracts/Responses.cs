using System;
using System.Collections.Generic;
using System.Text;

namespace OrderFlow.Contracts.Responses;

public record CreateOrderResponse(
    int Id,
    string Status);

public record GetOrderResponse(
    int Id,
    string Status,
    Guid ProductId,
    int Quantity);

public record SubmitOrderResponse(
    int Id,
    Guid CorrelationId,
    string Status);

