namespace OrderFlow.Inventory.Domain;

public enum ReservationStatus
{
    Reserved,
    Rejected,
    Released
}

public sealed class InventoryReservation
{
    public Guid CorrelationId { get; private set; }
    public Guid? ProductId { get; private set; }
    public int Quantity { get; private set; }
    public ReservationStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }

    private InventoryReservation() { }

    public static InventoryReservation Record(
        Guid correlationId,
        Guid productId,
        int quantity,
        string? rejectionReason)
    {
        return new InventoryReservation
        {
            CorrelationId = correlationId,
            ProductId = productId,
            Quantity = quantity,
            Status = rejectionReason is null
                ? ReservationStatus.Reserved
                : ReservationStatus.Rejected,
            RejectionReason = rejectionReason
        };
    }

    public static InventoryReservation RecordEarlyRelease(Guid correlationId)
    {
        return new InventoryReservation
        {
            CorrelationId = correlationId,
            Status = ReservationStatus.Released
        };
    }

    public void Release() => Status = ReservationStatus.Released;
}