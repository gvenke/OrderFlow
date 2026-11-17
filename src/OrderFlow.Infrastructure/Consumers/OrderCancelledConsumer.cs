
using MassTransit;
using OrderFlow.Application.Abstractions;
using OrderFlow.Contracts.Events;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Infrastructure.Consumers;

public class OrderCancelledConsumer : IConsumer<OrderCancelled>
{
    private readonly IOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public OrderCancelledConsumer(
        IOrderRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Consume(
        ConsumeContext<OrderCancelled> context)
    {
        var order = await _repository.GetByCorrelationIdAsync(
            context.Message.CorrelationId,
            context.CancellationToken);

        if (order is null)
            throw new InvalidOperationException(
                "Order not found for cancellation.");

        // Idempotent handling of duplicate events.
        if (order.Status == OrderStatus.Cancelled)
            return;

        // Domain method enforces valid state transitions.
        order.Cancel();

        await _unitOfWork.SaveChangesAsync(
            context.CancellationToken);
    }
}
