
using MassTransit;
using OrderFlow.Contracts.Events;

namespace OrderFlow.Orchestrator.Sagas;

public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public State AwaitingInventory { get; private set; } = null!;

    public Event<OrderSubmitted> OrderSubmitted { get; private set; } = null!;

    public Event<OrderCancellationRequested> OrderCancellationRequested { get; private set; } = null!;

    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderSubmitted, x =>
        {
            x.CorrelateById(context =>
                context.Message.CorrelationId);
        });

        Event(() => OrderCancellationRequested, x =>
        {
            x.CorrelateById(context =>
                context.Message.CorrelationId);

            x.OnMissingInstance(m => m.Discard());
        });

        Initially(
            When(OrderSubmitted)
                .Then(context =>
                {
                    context.Saga.ProductId =
                        context.Message.ProductId;

                    context.Saga.Quantity =
                        context.Message.Quantity;

                    context.Saga.SubmittedAtUtc =
                        DateTime.UtcNow;
                })
                .Publish(context =>
                    new OrderProcessingStarted(
                        context.Saga.CorrelationId))
                .TransitionTo(AwaitingInventory)
        );

        During(AwaitingInventory,
            When(OrderCancellationRequested)
                .Publish(context => new OrderCancelled(
                    context.Saga.CorrelationId))
                .Finalize()
        );

        SetCompletedWhenFinalized();
    }
}
