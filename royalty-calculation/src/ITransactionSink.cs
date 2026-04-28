namespace AspirePoc.RoyaltyCalculation;

public interface ITransactionSink
{
    Task WriteAsync(EnrichedTransaction transaction, Money discounted, CancellationToken ct);
}
