namespace AspirePoc.PreProcessor;

public record Batch(string BatchId, IReadOnlyList<Transaction> Transactions);
