
using MassTransit;
using OrderFlow.Application.Orders;
using OrderFlow.Contracts.Events;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Infrastructure.Consumers;

public class OrderStatusConsumer :
    IConsumer<OrderProcessingStarted>,
    IConsumer<OrderCompleted>,
    IConsumer<OrderFailed>,
    IConsumer<OrderCancelled>
{
    private readonly OrderEventService _service;

    public OrderStatusConsumer(OrderEventService service)
    {
        _service = service;
    }

    public Task Consume(
        ConsumeContext<OrderProcessingStarted> context)
    {
        return _service.ApplyAsync(
            context.Message.CorrelationId,
            order => order.StartProcessing(),
            context.CancellationToken);
    }

    public Task Consume(
        ConsumeContext<OrderCompleted> context)
    {
        return _service.ApplyAsync(
            context.Message.CorrelationId,
            order => order.Complete(),
            context.CancellationToken);
    }

    public Task Consume(ConsumeContext<OrderFailed> context)
    {
        return _service.ApplyAsync(
            context.Message.CorrelationId,
            order =>
            {
                // At this stage, OrderFailed means Inventory rejected the reservation.
                // If cancellation raced with rejection, there is no stock to release.
                if (order.Status == OrderStatus.Cancelling)
                    order.MarkCancelled();
                else if (order.Status != OrderStatus.Cancelled)
                    order.Fail();
            },
            context.CancellationToken);
    }

    public Task Consume(
        ConsumeContext<OrderCancelled> context)
    {
        return _service.ApplyAsync(
            context.Message.CorrelationId,
            order => order.MarkCancelled(),
            context.CancellationToken);
    }
}
