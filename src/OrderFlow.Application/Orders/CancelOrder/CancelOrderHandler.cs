
using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Orders.CancelOrder;
using OrderFlow.Contracts.Events;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Orders;

public class CancelOrderHandler
{
    private readonly IOrderRepository _repository;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IUnitOfWork _unitOfWork;

    public CancelOrderHandler(
        IOrderRepository repository,
        IIntegrationEventPublisher publisher,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _publisher = publisher;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(
        CancelOrderCommand command,
        CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdForUpdateAsync(
               command.Id, cancellationToken);

        if (order is null)
            return false;

        var wasCancelling = order.Status == OrderStatus.Cancelling;
        order.Cancel();

        if (!wasCancelling && order.Status == OrderStatus.Cancelling)
        {
            await _publisher.PublishAsync(
                new OrderCancellationRequested(order.CorrelationId),
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
