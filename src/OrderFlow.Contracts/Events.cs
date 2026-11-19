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

public record OrderProcessingStarted(
    Guid CorrelationId) : IMessage;


public record OrderCompleted(
    Guid CorrelationId) : IMessage;

public record OrderFailed(
    Guid CorrelationId,
    string Reason) : IMessage;