using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace AspirePoc.App2;

public static class KafkaTopicEnsurer
{
    private const int MaxAttempts = 30;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public static async Task EnsureAsync(
        string bootstrapServers,
        string topic,
        ILogger logger,
        CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var admin = new AdminClientBuilder(new AdminClientConfig
                {
                    BootstrapServers = bootstrapServers
                })
                    .SetLogHandler((_, _) => { })
                    .Build();

                try
                {
                    await admin.CreateTopicsAsync(new[]
                    {
                        new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }
                    });
                    logger.LogInformation("Created Kafka topic {Topic}", topic);
                }
                catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
                {
                    logger.LogInformation("Kafka topic {Topic} already exists", topic);
                }

                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                if (attempt == 1 || attempt % 5 == 0)
                {
                    logger.LogInformation(
                        "Waiting for Kafka broker (attempt {Attempt}/{Max}): {Message}",
                        attempt, MaxAttempts, ex.Message);
                }
                await Task.Delay(RetryDelay, ct);
            }
        }

        throw new InvalidOperationException(
            $"Failed to ensure Kafka topic {topic} after {MaxAttempts} attempts");
    }
}
