using OrderFlow.Application.Abstractions;

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
        var updated = await _repository.UpdateAsync(
            command.Id,
            command.ProductId,
            command.Quantity,
            cancellationToken);

        if (!updated)
            return false;

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return true;
    }
}
