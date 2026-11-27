using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using OrderFlow.SharedInfrastructure;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Inventory.Domain;

namespace OrderFlow.Inventory.Infrastructure;

public sealed class InventoryDbContext(
    DbContextOptions<InventoryDbContext> options, IConfiguration? configuration = null) : DbContext(options), IMessagingSchemaContext
{
    public string? MessagingSchema { get; } = TestMessaging.GetSchema(configuration);

    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<InventoryReservation> Reservations => Set<InventoryReservation>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, MessagingSchemaModelCacheKeyFactory>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StockItem>(entity =>
        {
            entity.ToTable("StockItems", table => table.HasCheckConstraint(
                "CK_StockItems_AvailableQuantity", "[AvailableQuantity] >= 0"));

            entity.HasKey(x => x.ProductId);
            entity.Property(x => x.ProductId).ValueGeneratedNever();
        });

        modelBuilder.Entity<InventoryReservation>(entity =>
        {
            entity.ToTable("InventoryReservations");
            entity.HasKey(x => x.CorrelationId);
            entity.Property(x => x.CorrelationId).ValueGeneratedNever();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.RejectionReason).HasMaxLength(200);
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