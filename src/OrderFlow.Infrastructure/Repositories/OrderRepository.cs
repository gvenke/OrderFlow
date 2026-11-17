
using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Persistence.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly OrderFlowDbContext _db;

    public OrderRepository(OrderFlowDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        await _db.Orders.AddAsync(
            order, cancellationToken);
    }

    public Task<Order?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }


    public Task<Order?> GetByIdForUpdateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _db.Orders
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }


    public Task<Order?> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return _db.Orders
            .FirstOrDefaultAsync(
                x => x.CorrelationId == correlationId,
                cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (order is null)
            return false;

        _db.Orders.Remove(order);
        return true;
    }

    public async Task<bool> UpdateAsync(
        int id,
        int quantity,
        CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (order is null)
            return false;

        order.UpdateQuantity(quantity);
        return true;
    }

    public async Task<IReadOnlyList<Order>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        return await _db.Orders
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }
}
