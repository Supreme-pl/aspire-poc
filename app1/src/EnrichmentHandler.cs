using System.Text.Json;
using Confluent.Kafka;

namespace AspirePoc.App1;

public sealed class EnrichmentHandler
{
    public const string EnrichedTopic = "transactions.enriched";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CustomerLookup customers;
    private readonly IProducer<string, string> producer;
    private readonly ILogger<EnrichmentHandler> logger;

    public EnrichmentHandler(
        CustomerLookup customers,
        IProducer<string, string> producer,
        ILogger<EnrichmentHandler> logger)
    {
        this.customers = customers;
        this.producer = producer;
        this.logger = logger;
    }

    public async Task<int> ProcessAsync(Batch batch, CancellationToken ct)
    {
        var published = 0;

        foreach (var transaction in batch.Transactions)
        {
            var customer = await customers.GetAsync(transaction.CustomerId, ct);
            if (customer is null)
            {
                logger.LogWarning(
                    "Unknown customer {CustomerId} for transaction {TransactionId}, skipping",
                    transaction.CustomerId,
                    transaction.TransactionId);
                continue;
            }

            var enriched = new EnrichedTransaction(
                transaction.TransactionId,
                transaction.CustomerId,
                customer.Name,
                customer.Tier,
                customer.DiscountRate,
                transaction.Amount,
                transaction.Currency);

            var message = new Message<string, string>
            {
                Key = transaction.TransactionId,
                Value = JsonSerializer.Serialize(enriched, JsonOptions)
            };

            await producer.ProduceAsync(EnrichedTopic, message, ct);
            published++;
        }

        logger.LogInformation(
            "Batch {BatchId}: published {Published}/{Total} enriched events",
            batch.BatchId,
            published,
            batch.Transactions.Count);

        return published;
    }
}
