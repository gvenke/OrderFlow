
using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Orders.CreateOrder;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Orders;

public class CreateOrderHandler
{
    private readonly IOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateOrderHandler(
        IOrderRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateOrderResult> Handle(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        var order = Order.Create(
            command.ProductId,
            command.Quantity);

        await _repository.AddAsync(
            order, cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return new CreateOrderResult(
            order.Id,
            order.CorrelationId,
            order.Status);
    }
}
