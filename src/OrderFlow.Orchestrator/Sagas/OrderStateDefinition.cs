
using MassTransit;
using OrderFlow.Orchestrator.Persistence;

namespace OrderFlow.Orchestrator.Sagas;

public class OrderStateDefinition
    : SagaDefinition<OrderState>
{
    public OrderStateDefinition()
    {
        Endpoint(e =>
        {
            e.Name = "order-saga";
            e.ConcurrentMessageLimit = 8;
        });
    }

    protected override void ConfigureSaga(
        IReceiveEndpointConfigurator endpointConfigurator,
        ISagaConfigurator<OrderState> sagaConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r =>
            r.Intervals(
                100,
                500,
                1000,
                2000));

        endpointConfigurator
            .UseEntityFrameworkOutbox<OrderSagaDbContext>(
                context);
    }
}
