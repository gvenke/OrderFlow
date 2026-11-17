
using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Orders.SubmitOrder;
using OrderFlow.Contracts.Events;


namespace OrderFlow.Application.Orders;

public class SubmitOrderHandler
{
    private readonly IOrderRepository _repository;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitOrderHandler(
        IOrderRepository repository,
        IIntegrationEventPublisher publisher,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _publisher = publisher;
        _unitOfWork = unitOfWork;
    }

    public async Task<SubmitOrderResult?> Handle(
        SubmitOrderCommand command,
        CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdForUpdateAsync(
            command.Id,
            cancellationToken);

        if (order is null)
            return null;

        // Domain validation and state transition
        order.Submit();

        // Stage the integration event in the Bus Outbox
        await _publisher.PublishAsync(
            new OrderSubmitted(
                order.CorrelationId,
                order.ProductId,
                order.Quantity),
            cancellationToken);

        // Commit the state change and event atomically
        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return new SubmitOrderResult(
            order.Id,
            order.CorrelationId,
            order.Status);
    }
}
