using System;
using System.Collections.Generic;
using System.Text;

namespace OrderFlow.Contracts.Events;

public interface IMessage
{
    Guid CorrelationId { get; }
}

public record OrderSubmitted(
    Guid CorrelationId,
    Guid ProductId,
    int Quantity) : IMessage;

public record OrderCancellationRequested(
    Guid CorrelationId) : IMessage;

public record OrderCancelled(
    Guid CorrelationId) : IMessage;

