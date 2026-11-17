
using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Orders.UpdateOrder;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Orders.UpdateOrder;

public class UpdateOrderHandler
{
    private readonly IOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateOrderHandler(
        IOrderRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(
        UpdateOrderCommand command,
        CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdForUpdateAsync(
            command.Id, cancellationToken);

        if (order is null)
            return false;

        order.UpdateQuantity(command.Quantity);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
