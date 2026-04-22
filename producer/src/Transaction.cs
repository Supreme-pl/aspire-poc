namespace AspirePoc.Producer;

public record Transaction(string TransactionId, string CustomerId, decimal Amount, string Currency);
