
using MassTransit;

namespace OrderFlow.Orchestrator.Sagas;

public class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }

    public string CurrentState { get; set; } = string.Empty;

    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    public DateTime SubmittedAtUtc { get; set; }
}
