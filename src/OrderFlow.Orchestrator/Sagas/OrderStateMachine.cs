using MassTransit;
using OrderFlow.Contracts.Events;

namespace OrderFlow.Orchestrator.Sagas;

public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    private static readonly Uri InventoryQueue = new("queue:inventory");

    public State AwaitingInventory { get; private set; } = null!;
    public State AwaitingPayment { get; private set; } = null!;
    public State ReleasingInventory { get; private set; } = null!;

    public Event<OrderSubmitted> OrderSubmitted { get; private set; } = null!;
    public Event<InventoryReserved> InventoryReserved { get; private set; } = null!;
    public Event<InventoryRejected> InventoryRejected { get; private set; } = null!;
    public Event<OrderCancellationRequested> OrderCancellationRequested { get; private set; } = null!;
    public Event<InventoryReleased> InventoryReleased { get; private set; } = null!;

    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderSubmitted, x =>
            x.CorrelateById(context => context.Message.CorrelationId));

        Event(() => OrderCancellationRequested, x =>
            x.CorrelateById(context => context.Message.CorrelationId));

        Event(() => InventoryReserved, x =>
        {
            x.CorrelateById(context => context.Message.CorrelationId);
            x.OnMissingInstance(m => m.Discard());
        });

        Event(() => InventoryRejected, x =>
        {
            x.CorrelateById(context => context.Message.CorrelationId);
            x.OnMissingInstance(m => m.Discard());
        });

        Event(() => InventoryReleased, x =>
        {
            x.CorrelateById(context => context.Message.CorrelationId);
            x.OnMissingInstance(m => m.Discard());
        });

        Initially(
            When(OrderSubmitted)
                .Then(context =>
                {
                    context.Saga.ProductId = context.Message.ProductId;
                    context.Saga.Quantity = context.Message.Quantity;
                    context.Saga.SubmittedAtUtc = DateTime.UtcNow;
                })
                .Publish(context => new OrderProcessingStarted(
                    context.Saga.CorrelationId))
                .Send(InventoryQueue, context => new ReserveInventory(
                    context.Saga.CorrelationId,
                    context.Saga.ProductId,
                    context.Saga.Quantity))
                .TransitionTo(AwaitingInventory),
            BeginRelease());

        During(AwaitingInventory,
            Ignore(OrderSubmitted),
            When(InventoryReserved).TransitionTo(AwaitingPayment),
            When(InventoryRejected)
                .Publish(context => new OrderFailed(
                    context.Saga.CorrelationId, context.Message.Reason))
                .Finalize(),
            BeginRelease());

        During(AwaitingPayment,
            Ignore(OrderSubmitted),
            Ignore(InventoryReserved),
            BeginRelease());

        During(ReleasingInventory,
            Ignore(OrderSubmitted),
            Ignore(OrderCancellationRequested),
            Ignore(InventoryReserved),
            Ignore(InventoryRejected),
            When(InventoryReleased)
                .Publish(context => new OrderCancelled(
                    context.Saga.CorrelationId))
                .Finalize());

        SetCompletedWhenFinalized();
    }

    private EventActivityBinder<OrderState, OrderCancellationRequested> BeginRelease() =>
        When(OrderCancellationRequested)
            .Send(InventoryQueue, context => new ReleaseInventory(
                context.Saga.CorrelationId))
            .TransitionTo(ReleasingInventory);
}