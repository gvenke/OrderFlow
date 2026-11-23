namespace OrderFlow.Inventory.Domain;

public sealed class StockItem
{
    public Guid ProductId { get; private set; }
    public int AvailableQuantity { get; private set; }

    public bool TryReserve(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        if (AvailableQuantity < quantity)
            return false;

        AvailableQuantity -= quantity;
        return true;
    }

    public void Return(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        AvailableQuantity = checked(AvailableQuantity + quantity);
    }
}