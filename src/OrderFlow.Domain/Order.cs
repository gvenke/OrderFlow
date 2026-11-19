
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Domain.Orders;

public class Order
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; private set; }

    public Guid CorrelationId { get; private set; } = Guid.NewGuid();
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }

    // enforces optimistic ocncurrency
    public byte[] RowVersion { get; private set; } = [];

    public OrderStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; private set; }

    // Required by EF Core
    private Order() { }

    public static Order Create(
        Guid productId,
        int quantity)
    {
        if (productId == Guid.Empty)
            throw new DomainValidationException(
                "Product ID is required.");

        if (quantity <= 0)
            throw new DomainValidationException(
                "Quantity must be positive.");

        return new Order
        {
            ProductId = productId,
            Quantity = quantity,
            Status = OrderStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void UpdateQuantity(
        int quantity)


    {

        if (Status != OrderStatus.Draft)
        {
            throw new BusinessRuleException(
                "Only draft orders can be updated.");
        }

        if (quantity <= 0)
            throw new DomainValidationException(
                "Quantity must be positive.");

        Quantity = quantity;
    }

    public void Submit()
    {
        if (Status != OrderStatus.Draft)
            throw new BusinessRuleException(
                "Only draft orders can be submitted.");

        Status = OrderStatus.Pending;
    }

    public void StartProcessing()
    {
        if (Status != OrderStatus.Pending)
            throw new BusinessRuleException(
                "Only pending orders can be processed.");

        Status = OrderStatus.Processing;
    }

    public void Complete()
    {
        if (Status == OrderStatus.Completed)
            return;

        if (Status != OrderStatus.Processing)
            throw new BusinessRuleException(
                "Only processing orders can be completed.");

        Status = OrderStatus.Completed;
    }

    public void Fail()
    {
        
        if (Status == OrderStatus.Failed)
            return;

        if (Status is OrderStatus.Completed
        or OrderStatus.Cancelling
        or OrderStatus.Cancelled)
        throw new BusinessRuleException(
            "Cannot fail a finalized order.");

        Status = OrderStatus.Failed;
    }

    public void Cancel()
    {
    if (Status == OrderStatus.Cancelled ||
        Status == OrderStatus.Cancelling)
        return;

    if (Status == OrderStatus.Draft)
    {
        Status = OrderStatus.Cancelled;
        return;
    }

    if (Status is OrderStatus.Completed
        or OrderStatus.Failed)
    {
        throw new BusinessRuleException(
            "Order cannot be cancelled in its current state.");
    }
    }

    public void MarkCancelled()
    {
        if (Status == OrderStatus.Cancelled)
            return;

        if (Status != OrderStatus.Cancelling)
        {
            throw new BusinessRuleException(
                "Only cancelling orders can be marked as cancelled.");
        }

        Status = OrderStatus.Cancelled;
    }
}


public enum OrderStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelling,
    Cancelled,
    Draft
}