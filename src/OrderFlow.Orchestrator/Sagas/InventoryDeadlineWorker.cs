using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Orchestrator.Persistence;

namespace OrderFlow.Orchestrator.Sagas;

public record InventoryDeadlineExpired(Guid CorrelationId, DateTime DeadlineUtc, int Attempt);

public sealed class InventoryDeadlineWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<InventoryDeadlineWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<OrderSagaDbContext>();
                var now = DateTime.UtcNow;
                var expired = await db.Set<OrderState>()
                    .AsNoTracking()
                    .Where(state => state.InventoryDeadlineUtc <= now)
                    .OrderBy(state => state.InventoryDeadlineUtc)
                    .Take(100)
                    .Select(state => new InventoryDeadlineExpired(
                        state.CorrelationId, state.InventoryDeadlineUtc!.Value, state.InventoryAttempts))
                    .ToListAsync(stoppingToken);

                if (expired.Count == 0)
                    continue;

                var provider = scope.ServiceProvider.GetRequiredService<ISendEndpointProvider>();
                var endpoint = await provider.GetSendEndpoint(new Uri("queue:order-saga"));
                foreach (var message in expired)
                    await endpoint.Send(message, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Could not dispatch due inventory deadlines.");
            }
        }
    }
}
