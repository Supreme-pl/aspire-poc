using System.Text.Json;
using Confluent.Kafka;

namespace AspirePoc.RoyaltyCalculation;

public sealed class KafkaConsumerService : BackgroundService
{
    public const string DefaultTopic = "transactions.enriched";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConsumer<string, string> consumer;
    private readonly ITransactionSink sink;
    private readonly ILogger<KafkaConsumerService> logger;
    private readonly string topic;

    public KafkaConsumerService(
        IConsumer<string, string> consumer,
        ITransactionSink sink,
        IConfiguration config,
        ILogger<KafkaConsumerService> logger)
    {
        this.consumer = consumer;
        this.sink = sink;
        this.logger = logger;
        this.topic = config[Constants.KafkaTopicConfigKey] ?? DefaultTopic;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        consumer.Subscribe(topic);
        logger.LogInformation("Subscribed to topic {Topic}", topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result;
                try
                {
                    result = consumer.Consume(stoppingToken);
                }
                catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                {
                    logger.LogDebug("Topic {Topic} not yet available, retrying", topic);
                    continue;
                }

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

                var money = new Money(transaction.Amount, transaction.Currency);
                var discounted = money.ApplyDiscount(transaction.DiscountRate);
                await sink.WriteAsync(transaction, discounted, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            consumer.Close();
        }
    }
}
