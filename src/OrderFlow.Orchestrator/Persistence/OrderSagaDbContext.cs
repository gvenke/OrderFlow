using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using OrderFlow.SharedInfrastructure;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Orchestrator.Sagas;

namespace OrderFlow.Orchestrator.Persistence;

public class OrderSagaDbContext : SagaDbContext, IMessagingSchemaContext
{
    public OrderSagaDbContext(
        DbContextOptions<OrderSagaDbContext> options, IConfiguration? configuration = null)
        : base(options)
    {
        MessagingSchema = TestMessaging.GetSchema(configuration);
    }

    public string? MessagingSchema { get; }

    protected override IEnumerable<ISagaClassMap>
        Configurations
    {
        get { yield return new OrderStateMap(); }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, MessagingSchemaModelCacheKeyFactory>();
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
            .ToTable("SagaInboxState", MessagingSchema);

        modelBuilder.Entity<OutboxMessage>()
            .ToTable("SagaOutboxMessage", MessagingSchema);

        modelBuilder.Entity<OutboxState>()
            .ToTable("SagaOutboxState", MessagingSchema);

        modelBuilder.Entity<OrderState>().ToTable("OrderSagaStates", MessagingSchema);
    }
}
