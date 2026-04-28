using System.Text.Json;
using Confluent.Kafka;

namespace AspirePoc.Indexer;

public sealed class KafkaConsumerService : BackgroundService
{
    public const string DefaultTopic = "transactions.enriched";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConsumer<string, string> consumer;
    private readonly OpenSearchIndexer indexer;
    private readonly ILogger<KafkaConsumerService> logger;
    private readonly string topic;

    public KafkaConsumerService(
        IConsumer<string, string> consumer,
        OpenSearchIndexer indexer,
        IConfiguration config,
        ILogger<KafkaConsumerService> logger)
    {
        this.consumer = consumer;
        this.indexer = indexer;
        this.logger = logger;
        this.topic = config["Kafka:Topic"] ?? DefaultTopic;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        consumer.Subscribe(topic);
        logger.LogInformation("Indexer subscribed to topic {Topic}", topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    if (result?.Message?.Value is null)
                    {
                        continue;
                    }

                    var transaction = JsonSerializer.Deserialize<EnrichedTransaction>(result.Message.Value, JsonOptions);
                    if (transaction is null)
                    {
                        logger.LogWarning("Skipping message with empty payload at offset {Offset}", result.Offset);
                        continue;
                    }

                    await indexer.IndexAsync(transaction, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ConsumeException ex)
                {
                    logger.LogError(ex, "Kafka consume error: {Reason}", ex.Error.Reason);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to index Kafka message");
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }
}
