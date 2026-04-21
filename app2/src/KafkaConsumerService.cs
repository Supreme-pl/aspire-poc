using System.Text.Json;
using Confluent.Kafka;

namespace AspirePoc.App2;

public sealed class KafkaConsumerService : BackgroundService
{
    public const string EnrichedTopic = "transactions.enriched";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConsumer<string, string> consumer;
    private readonly TransactionProcessor processor;
    private readonly ILogger<KafkaConsumerService> logger;

    public KafkaConsumerService(
        IConsumer<string, string> consumer,
        TransactionProcessor processor,
        ILogger<KafkaConsumerService> logger)
    {
        this.consumer = consumer;
        this.processor = processor;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        consumer.Subscribe(EnrichedTopic);
        logger.LogInformation("Subscribed to topic {Topic}", EnrichedTopic);

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

                    await processor.ProcessAsync(transaction, stoppingToken);
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
                    logger.LogError(ex, "Failed to process Kafka message");
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }
}
