using OrderFlow.Contracts;
using OrderFlow.Contracts.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace OrderFlow.Application.Abstractions
{
    public interface IIntegrationEventPublisher
    {
        Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T: IEvent;
    }


}
