
using OrderFlow.Application.Abstractions;
using OrderFlow.Domain.Orders;
using OrderFlow.Contracts;
using OrderFlow.Contracts.Events;

namespace OrderFlow.Application.Orders.CreateOrder;

public class CreateOrderHandler
{
    private readonly IOrderRepository _repository;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IUnitOfWork _unitOfWork;

    public CreateOrderHandler(
        IOrderRepository repository,
        IIntegrationEventPublisher publisher,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _publisher = publisher;
        _unitOfWork = unitOfWork;
    }


    public async Task<CreateOrderResult> Handle(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Create the domain aggregate.
        var order = Order.Create(
            command.ProductId,
            command.Quantity);

        // 2. Track it in the EF Core DbContext.
        await _repository.AddAsync(
            order,
            cancellationToken);


        // 3. Stage the integration event in the Bus Outbox.
        await _publisher.PublishAsync(new OrderSubmitted(
                order.CorrelationId,
                order.ProductId,
                order.Quantity),
                cancellationToken);

        // 4. Commit the Order and outgoing event together.
        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        // 5. SQL Server has now generated the integer ID.
        return new CreateOrderResult(
            order.Id,
            order.CorrelationId,
            order.Status);
    }

}
