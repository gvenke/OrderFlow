using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using OrderFlow.SharedInfrastructure;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Persistence;

public class OrderFlowDbContext : DbContext, IMessagingSchemaContext
{
    public OrderFlowDbContext(
        DbContextOptions<OrderFlowDbContext> options, IConfiguration? configuration = null)
        : base(options)
    {
        MessagingSchema = TestMessaging.GetSchema(configuration);
    }

    public string? MessagingSchema { get; }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, MessagingSchemaModelCacheKeyFactory>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");

            entity.Property(x => x.ProductId)
                .IsRequired();

            entity.Property(x => x.Quantity)
                .IsRequired();

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.Property(x => x.RowVersion)
                .IsRowVersion();

            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasIndex(x => x.CorrelationId).IsUnique();
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        if (MessagingSchema is not null)
        {
            modelBuilder.Entity<InboxState>().ToTable("InboxState", MessagingSchema);
            modelBuilder.Entity<OutboxMessage>().ToTable("OutboxMessage", MessagingSchema);
            modelBuilder.Entity<OutboxState>().ToTable("OutboxState", MessagingSchema);
        }
    }
}
