namespace OrderFlow.Contracts;





public record ReserveInventory(
    int OrderId,
    Guid ProductId,
    int Quantity);

public record InventoryReserved(int OrderId);

public record InventoryRejected(
    int OrderId,
    string Reason);

public record ProcessPayment(
    int OrderId,
    decimal Amount);

public record PaymentCompleted(int OrderId);

public record PaymentFailed(
    int OrderId,
    string Reason);

public record ReleaseInventory(int OrderId);

public record InventoryReleased(int OrderId);

public record OrderCompleted(int OrderId);

public record OrderFailed(
    int OrderId,
    string Reason);

// api rerquest/response



