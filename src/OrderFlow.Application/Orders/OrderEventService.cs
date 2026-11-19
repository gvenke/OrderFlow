
using OrderFlow.Application.Abstractions;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Orders;

public class OrderEventService
{
    private readonly IOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public OrderEventService(
        IOrderRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task ApplyAsync(
        Guid correlationId,
        Action<Order> transition,
        CancellationToken cancellationToken)
    {
        var order =
            await _repository.GetByCorrelationIdForUpdateAsync(
                correlationId, cancellationToken);

        if (order is null)
            throw new KeyNotFoundException(
                $"Order not found: {correlationId}");

        transition(order);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);
    }
}
