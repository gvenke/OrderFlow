
using MassTransit;
using OrderFlow.Persistence;

namespace OrderFlow.Infrastructure.Consumers;

public class OrderStatusConsumerDefinition
    : ConsumerDefinition<OrderStatusConsumer>
{
    public OrderStatusConsumerDefinition()
    {
        Endpoint(e => e.Name = "order-status");
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<OrderStatusConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r =>
            r.Intervals(100, 500, 1000));

        endpointConfigurator
            .UseEntityFrameworkOutbox<OrderFlowDbContext>(
                context);
    }
}
