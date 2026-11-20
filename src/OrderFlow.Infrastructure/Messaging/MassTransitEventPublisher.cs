
using MassTransit;
using OrderFlow.Application.Abstractions;
using OrderFlow.Contracts.Events;

namespace OrderFlow.Persistence.Messaging;

public class MassTransitEventPublisher
    : IIntegrationEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitEventPublisher(
        IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : IEvent
    {
        return _publishEndpoint.Publish(message, cancellationToken);
    }

}
