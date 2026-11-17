
using OrderFlow.Application.Abstractions;

namespace OrderFlow.Persistence;

public class PersistenceUnitOfWork : IUnitOfWork
{
    private readonly OrderFlowDbContext _db;

    public PersistenceUnitOfWork(OrderFlowDbContext db)
    {
        _db = db;
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        await _db.SaveChangesAsync(cancellationToken);
    }
}
