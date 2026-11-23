using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Contracts.Events;
using OrderFlow.Inventory.Domain;
using OrderFlow.Inventory.Infrastructure;

namespace OrderFlow.Inventory.Consumers;

public sealed class InventoryConsumer(InventoryDbContext db) :
    IConsumer<ReserveInventory>,
    IConsumer<ReleaseInventory>
{
    public async Task Consume(ConsumeContext<ReserveInventory> context)
    {
        var message = context.Message;
        var cancellationToken = context.CancellationToken;

        var reservation = await db.Reservations.SingleOrDefaultAsync(
            x => x.CorrelationId == message.CorrelationId, cancellationToken);

        if (reservation is null)
        {
            var reason = await TryReserveAsync(message, cancellationToken);

            reservation = InventoryReservation.Record(
                message.CorrelationId, message.ProductId, message.Quantity, reason);

            db.Reservations.Add(reservation);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (reservation.Status == ReservationStatus.Reserved)
        {
            await context.Publish(
                new InventoryReserved(message.CorrelationId), cancellationToken);
            return;
        }

        var rejectionReason = reservation.Status == ReservationStatus.Released
            ? "The inventory reservation has been released."
            : reservation.RejectionReason!;

        await context.Publish(
            new InventoryRejected(message.CorrelationId, rejectionReason),
            cancellationToken);
    }

    public async Task Consume(ConsumeContext<ReleaseInventory> context)
    {
        var correlationId = context.Message.CorrelationId;
        var cancellationToken = context.CancellationToken;

        var reservation = await db.Reservations.SingleOrDefaultAsync(
            x => x.CorrelationId == correlationId, cancellationToken);

        if (reservation is null)
        {
            // Cancellation can reach Inventory before the reservation command.
            db.Reservations.Add(InventoryReservation.RecordEarlyRelease(correlationId));
        }
        else
        {
            if (reservation.Status == ReservationStatus.Reserved)
            {
                var stock = await db.StockItems.SingleAsync(
                    x => x.ProductId == reservation.ProductId, cancellationToken);

                stock.Return(reservation.Quantity);
            }

            reservation.Release();
        }

        await db.SaveChangesAsync(cancellationToken);
        await context.Publish(new InventoryReleased(correlationId), cancellationToken);
    }

    private async Task<string?> TryReserveAsync(
        ReserveInventory command, CancellationToken cancellationToken)
    {
        if (command.ProductId == Guid.Empty || command.Quantity <= 0)
            return "The reservation requires a product and a positive quantity.";

        var stock = await db.StockItems.SingleOrDefaultAsync(
            x => x.ProductId == command.ProductId, cancellationToken);

        if (stock is null)
            return "The product is missing from Inventory.";

        return stock.TryReserve(command.Quantity)
            ? null
            : "Insufficient available stock.";
    }
}