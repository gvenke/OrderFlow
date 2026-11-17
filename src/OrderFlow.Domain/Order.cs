
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderFlow.Domain.Orders;

public class Order
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; private set; }

    public Guid CorrelationId { get; private set; } = Guid.NewGuid();
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }

    public OrderStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; private set; }

    // Required by EF Core
    private Order() { }

    public static Order Create(
        Guid productId,
        int quantity)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException(
                "Product ID is required.");

        if (quantity <= 0)
            throw new ArgumentException(
                "Quantity must be positive.");

        return new Order
        {
            ProductId = productId,
            Quantity = quantity,
            Status = OrderStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void UpdateDetails(
        Guid productId,
        int quantity)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException(
                "Product ID is required.");

        if (quantity <= 0)
            throw new ArgumentException(
                "Quantity must be positive.");

        ProductId = productId;
        Quantity = quantity;
    }

    public void StartProcessing()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException(
                "Only pending orders can be processed.");

        Status = OrderStatus.Processing;
    }

    public void Complete()
    {
        if (Status != OrderStatus.Processing)
            throw new InvalidOperationException(
                "Order must be processing.");

        Status = OrderStatus.Completed;
    }

    public void Fail()
    {
        if (Status is OrderStatus.Completed
            or OrderStatus.Cancelling
            or OrderStatus.Cancelled)
            throw new InvalidOperationException(
                "Cannot fail a finalized order.");

        Status = OrderStatus.Failed;
    }

    public void Cancel()
    {

        if (Status is OrderStatus.Completed
            or OrderStatus.Cancelling
            or OrderStatus.Cancelled
            or OrderStatus.Failed)
        {
            throw new InvalidOperationException(
                "Order cannot be cancelled in its current state.");
        }

       Status = OrderStatus.Cancelling;
    }
}


public enum OrderStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelling,
    Cancelled
}