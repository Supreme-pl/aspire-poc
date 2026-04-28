namespace AspirePoc.App2;

public interface ITransactionSink
{
    Task WriteAsync(EnrichedTransaction transaction, Money discounted, CancellationToken ct);
}
