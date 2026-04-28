using OpenSearch.Client;

namespace AspirePoc.Indexer;

public sealed class OpenSearchIndexer
{
    public const string DefaultIndexName = "transactions";

    private readonly IOpenSearchClient client;
    private readonly string indexName;
    private readonly ILogger<OpenSearchIndexer> logger;

    public OpenSearchIndexer(
        IOpenSearchClient client,
        IConfiguration config,
        ILogger<OpenSearchIndexer> logger)
    {
        this.client = client;
        this.indexName = config["OpenSearch:Index"] ?? DefaultIndexName;
        this.logger = logger;
    }

    public async Task IndexAsync(EnrichedTransaction transaction, CancellationToken ct)
    {
        var response = await client.IndexAsync(
            transaction,
            descriptor => descriptor.Index(indexName).Id(transaction.TransactionId),
            ct);

        if (!response.IsValid)
        {
            logger.LogWarning(
                "Failed to index transaction {TransactionId}: {Error}",
                transaction.TransactionId,
                response.ServerError?.Error?.Reason ?? response.DebugInformation);
            return;
        }

        logger.LogDebug(
            "Indexed transaction {TransactionId} into {Index}",
            transaction.TransactionId, indexName);
    }
}
