using MassTransit;
using OrderFlow.Contracts.Events;

namespace OrderFlow.Orchestrator.Sagas;

public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    private static readonly Uri InventoryQueue = new("queue:inventory");
    private const int MaxInventoryAttempts = 3;

    public State AwaitingInventory { get; private set; } = null!;
    public State AwaitingPayment { get; private set; } = null!;
    public State ReleasingInventory { get; private set; } = null!;
    public State ReservationNeedsRecovery { get; private set; } = null!;
    public State ReleaseNeedsRecovery { get; private set; } = null!;

    public Event<OrderSubmitted> OrderSubmitted { get; private set; } = null!;
    public Event<InventoryReserved> InventoryReserved { get; private set; } = null!;
    public Event<InventoryRejected> InventoryRejected { get; private set; } = null!;
    public Event<OrderCancellationRequested> OrderCancellationRequested { get; private set; } = null!;
    public Event<InventoryReleased> InventoryReleased { get; private set; } = null!;
    public Event<InventoryDeadlineExpired> InventoryDeadlineExpired { get; private set; } = null!;
    public Event<Fault<ReserveInventory>> ReserveInventoryFaulted { get; private set; } = null!;
    public Event<Fault<ReleaseInventory>> ReleaseInventoryFaulted { get; private set; } = null!;

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

        Event(() => InventoryDeadlineExpired, x =>
        {
            x.CorrelateById(context => context.Message.CorrelationId);
            x.OnMissingInstance(m => m.Discard());
        });

        Event(() => ReserveInventoryFaulted, x =>
        {
            x.CorrelateById(context => context.Message.Message.CorrelationId);
            x.OnMissingInstance(m => m.Discard());
        });

        Event(() => ReleaseInventoryFaulted, x =>
        {
            x.CorrelateById(context => context.Message.Message.CorrelationId);
            x.OnMissingInstance(m => m.Discard());
        });

        Initially(
            When(OrderSubmitted)
                .Then(context =>
                {
                    context.Saga.ProductId = context.Message.ProductId;
                    context.Saga.Quantity = context.Message.Quantity;
                    context.Saga.SubmittedAtUtc = DateTime.UtcNow;
                    context.Saga.StartInventoryOperation();
                })
                .Publish(context => new OrderProcessingStarted(
                    context.Saga.CorrelationId))
                .Send(InventoryQueue, context => Reserve(context.Saga))
                .TransitionTo(AwaitingInventory),
            BeginRelease());

        During(AwaitingInventory,
            RetryInventory(ReservationNeedsRecovery, retry =>
                retry.Send(InventoryQueue, context => Reserve(context.Saga))),
            When(ReserveInventoryFaulted)
                .Then(context => context.Saga.InventoryRecoveryReason = "ReserveInventory faulted."));

        During(AwaitingInventory, ReservationNeedsRecovery,
            Ignore(OrderSubmitted),
            Ignore(ReleaseInventoryFaulted),
            When(InventoryReserved)
                .Then(context => context.Saga.ClearInventoryRecovery())
                .TransitionTo(AwaitingPayment),
            When(InventoryRejected)
                .Publish(context => new OrderFailed(
                    context.Saga.CorrelationId, context.Message.Reason))
                .Finalize(),
            BeginRelease());

        During(AwaitingPayment,
            Ignore(OrderSubmitted),
            Ignore(InventoryReserved),
            Ignore(InventoryRejected),
            Ignore(InventoryDeadlineExpired),
            Ignore(ReserveInventoryFaulted),
            Ignore(ReleaseInventoryFaulted),
            BeginRelease());

        During(ReleasingInventory,
            RetryInventory(ReleaseNeedsRecovery, retry =>
                retry.Send(InventoryQueue, context => new ReleaseInventory(context.Saga.CorrelationId))),
            When(ReleaseInventoryFaulted)
                .Then(context => context.Saga.InventoryRecoveryReason = "ReleaseInventory faulted."));

        During(ReleasingInventory, ReleaseNeedsRecovery,
            Ignore(OrderSubmitted),
            Ignore(OrderCancellationRequested),
            Ignore(InventoryReserved),
            Ignore(InventoryRejected),
            Ignore(ReserveInventoryFaulted),
            When(InventoryReleased)
                .Publish(context => new OrderCancelled(
                    context.Saga.CorrelationId))
                .Finalize());

        During(ReservationNeedsRecovery,
            Ignore(InventoryDeadlineExpired),
            Ignore(ReserveInventoryFaulted));

        During(ReleaseNeedsRecovery,
            Ignore(InventoryDeadlineExpired),
            Ignore(ReleaseInventoryFaulted));

        SetCompletedWhenFinalized();
    }

    private EventActivityBinder<OrderState, OrderCancellationRequested> BeginRelease() =>
        When(OrderCancellationRequested)
            .Then(context => context.Saga.StartInventoryOperation())
            .Send(InventoryQueue, context => new ReleaseInventory(
                context.Saga.CorrelationId))
            .TransitionTo(ReleasingInventory);

    private EventActivityBinder<OrderState, InventoryDeadlineExpired> RetryInventory(
        State recoveryState,
        Func<EventActivityBinder<OrderState, InventoryDeadlineExpired>,
            EventActivityBinder<OrderState, InventoryDeadlineExpired>> sendRetry) =>
        When(InventoryDeadlineExpired, context =>
                context.Saga.InventoryDeadlineUtc == context.Message.DeadlineUtc &&
                context.Saga.InventoryAttempts == context.Message.Attempt)
            .IfElse(context => context.Saga.InventoryAttempts < MaxInventoryAttempts,
                retry => sendRetry(retry.Then(context => context.Saga.RecordInventoryAttempt())),
                exhausted => exhausted
                    .Then(context =>
                    {
                        context.Saga.InventoryDeadlineUtc = null;
                        context.Saga.InventoryRecoveryReason ??= "No inventory reply before the final deadline.";
                    })
                    .TransitionTo(recoveryState));

    private static ReserveInventory Reserve(OrderState state) =>
        new(state.CorrelationId, state.ProductId, state.Quantity);
}
