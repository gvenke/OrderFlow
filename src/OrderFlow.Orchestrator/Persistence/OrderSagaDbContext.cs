
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Orchestrator.Sagas;

namespace OrderFlow.Orchestrator.Persistence;

public class OrderSagaDbContext : SagaDbContext
{
    public OrderSagaDbContext(
        DbContextOptions<OrderSagaDbContext> options)
        : base(options)
    {
    }

    protected override IEnumerable<ISagaClassMap>
        Configurations
    {
        get { yield return new OrderStateMap(); }
    }

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Unique names because the Orders API
        // uses the same development database.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.Entity<InboxState>()
            .ToTable("SagaInboxState");

        modelBuilder.Entity<OutboxMessage>()
            .ToTable("SagaOutboxMessage");

        modelBuilder.Entity<OutboxState>()
            .ToTable("SagaOutboxState");
    }
}
