namespace AspirePoc.Indexer;

internal static class Constants
{
    public const string KafkaConnectionName = "kafka";
    public const string OpenSearchConnectionName = "opensearch";
    public const string DefaultConsumerGroup = "indexer-consumer-group";
    public const string KafkaTopicConfigKey = "Kafka:Topic";
    public const string OpenSearchIndexConfigKey = "OpenSearch:Index";
}
