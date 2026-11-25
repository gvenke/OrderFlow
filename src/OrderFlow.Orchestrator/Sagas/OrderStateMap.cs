
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OrderFlow.Orchestrator.Sagas;

public class OrderStateMap : SagaClassMap<OrderState>
{
    protected override void Configure(
        EntityTypeBuilder<OrderState> entity,
        ModelBuilder model)
    {
        entity.ToTable("OrderSagaStates");

        entity.Property(x => x.CurrentState)
            .HasMaxLength(64);

        entity.Property(x => x.ProductId)
            .IsRequired();

        entity.Property(x => x.Quantity)
            .IsRequired();

        entity.Property(x => x.SubmittedAtUtc)
            .IsRequired();

        entity.HasIndex(x => x.InventoryDeadlineUtc)
            .HasFilter("[InventoryDeadlineUtc] IS NOT NULL");
        entity.Property(x => x.InventoryRecoveryReason).HasMaxLength(200);
    }
}
