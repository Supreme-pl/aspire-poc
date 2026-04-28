namespace AspirePoc.Gch;

public record Batch(string BatchId, IReadOnlyList<Transaction> Transactions);
