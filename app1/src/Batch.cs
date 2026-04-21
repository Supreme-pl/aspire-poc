namespace AspirePoc.App1;

public record Batch(string BatchId, IReadOnlyList<Transaction> Transactions);
