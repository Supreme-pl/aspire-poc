namespace AspirePoc.Producer;

public record Batch(string BatchId, IReadOnlyList<Transaction> Transactions);
