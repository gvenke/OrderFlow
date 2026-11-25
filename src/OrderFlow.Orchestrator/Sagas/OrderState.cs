
using MassTransit;

namespace OrderFlow.Orchestrator.Sagas;

public class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }

    public string CurrentState { get; set; } = string.Empty;

    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    public DateTime SubmittedAtUtc { get; set; }

    public DateTime? InventoryDeadlineUtc { get; set; }

    public int InventoryAttempts { get; set; }

    public string? InventoryRecoveryReason { get; set; }

    public void StartInventoryOperation()
    {
        InventoryAttempts = 0;
        InventoryRecoveryReason = null;
        RecordInventoryAttempt();
    }

    public void RecordInventoryAttempt()
    {
        InventoryAttempts++;
        InventoryDeadlineUtc = DateTime.UtcNow.AddSeconds(15);
    }

    public void ClearInventoryRecovery()
    {
        InventoryDeadlineUtc = null;
        InventoryRecoveryReason = null;
    }
}
