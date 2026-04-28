namespace AspirePoc.Gch;

public sealed class BatchGenerator
{
    private static readonly string[] CustomerPool =
    [
        "C-100", "C-100", "C-100", "C-100", "C-100", "C-100", "C-100", "C-100",
        "C-101", "C-101", "C-101", "C-101",
        "C-102", "C-102", "C-102",
        "C-103", "C-103",
        "C-104", "C-104",
        "C-999"
    ];

    private static readonly string[] Currencies = ["USD", "EUR", "GBP", "PLN", "JPY"];

    public Batch Generate(int batchSize)
    {
        var batchId = $"B-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 10000)}";
        var transactions = new List<Transaction>(batchSize);

        for (var i = 0; i < batchSize; i++)
        {
            transactions.Add(new Transaction(
                TransactionId: $"T-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                CustomerId: CustomerPool[Random.Shared.Next(CustomerPool.Length)],
                Amount: RandomAmount(),
                Currency: Currencies[Random.Shared.Next(Currencies.Length)]));
        }

        return new Batch(batchId, transactions);
    }

    private static decimal RandomAmount()
    {
        var cents = Random.Shared.Next(100, 1_000_000);
        return cents / 100m;
    }
}
