namespace OrderFlow.Contracts;






public record ProcessPayment(
    int OrderId,
    decimal Amount);

public record PaymentCompleted(int OrderId);

public record PaymentFailed(
    int OrderId,
    string Reason);



public record OrderCompleted(int OrderId);

public record OrderFailed(
    int OrderId,
    string Reason);

// api rerquest/response



