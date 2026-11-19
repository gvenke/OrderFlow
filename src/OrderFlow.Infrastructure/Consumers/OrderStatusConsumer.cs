
using MassTransit;
using OrderFlow.Application.Orders;
using OrderFlow.Contracts.Events;

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

    public Task Consume(
        ConsumeContext<OrderFailed> context)
    {
        return _service.ApplyAsync(
            context.Message.CorrelationId,
            order => order.Fail(),
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
