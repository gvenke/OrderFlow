
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
        // 1. verify order
        var order = await _repository.GetByIdAsync(
            command.Id,
            cancellationToken);

        if (order is null) return false;
        bool publish = (order.Status != OrderStatus.Draft);
        order.Cancel();

        // no need to publish if the order was in "draft" stage. It was already cancelled in "order.Cancel"
        if (publish) {
            await _publisher.PublishAsync(new OrderCancellationRequested(
            order.CorrelationId),
            cancellationToken);
        }
       
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
