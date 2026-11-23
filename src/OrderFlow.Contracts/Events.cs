using System;
using System.Collections.Generic;
using System.Text;

namespace OrderFlow.Contracts.Events;

public interface IEvent
{
    Guid CorrelationId { get; }
}

public record OrderSubmitted(
    Guid CorrelationId,
    Guid ProductId,
    int Quantity) : IEvent;

public record OrderCancellationRequested(
    Guid CorrelationId) : IEvent;

public record OrderCancelled(
    Guid CorrelationId) : IEvent;

public record OrderProcessingStarted(
    Guid CorrelationId) : IEvent;


public record OrderCompleted(
    Guid CorrelationId) : IEvent;

public record OrderFailed(
    Guid CorrelationId,
    string Reason) : IEvent;

public record ReserveInventory(
    Guid CorrelationId,
    Guid ProductId,
    int Quantity) : IEvent;

public record InventoryReserved(Guid CorrelationId) : IEvent;

public record InventoryRejected(
    Guid CorrelationId,
    string Reason) : IEvent;

public record ReleaseInventory(Guid CorrelationId) : IEvent;

public record InventoryReleased(Guid CorrelationId) : IEvent;


