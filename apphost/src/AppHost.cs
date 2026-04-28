using Microsoft.Extensions.Configuration;

const string KafkaTopicKey = "Kafka:Topic";
const string KafkaConsumerGroupKey = "Kafka:ConsumerGroup";
const string OutputPathKey = "Output:Path";
const string ProducerEnabledKey = "Producer:Enabled";
const string KafkaUiEnabledKey = "KafkaUI:Enabled";
const string RedisInsightEnabledKey = "RedisInsight:Enabled";
const string OpenSearchEnabledKey = "OpenSearch:Enabled";
const string OpenSearchDashboardsEnabledKey = "OpenSearchDashboards:Enabled";

var builder = DistributedApplication.CreateBuilder(args);

var topic = builder.Configuration[KafkaTopicKey] ?? "transactions.enriched";
var consumerGroup = builder.Configuration[KafkaConsumerGroupKey] ?? "app2-consumer-group";
var outputPath = builder.Configuration[OutputPathKey];
var producerEnabled = ReadFlag(builder.Configuration, ProducerEnabledKey);
var kafkaUiEnabled = ReadFlag(builder.Configuration, KafkaUiEnabledKey);
var redisInsightEnabled = ReadFlag(builder.Configuration, RedisInsightEnabledKey);
var opensearchEnabled = ReadFlag(builder.Configuration, OpenSearchEnabledKey);
var opensearchDashboardsEnabled = ReadFlag(builder.Configuration, OpenSearchDashboardsEnabledKey);

var cacheBuilder = builder.AddRedis("cache");
if (redisInsightEnabled)
{
    cacheBuilder.WithRedisInsight();
}
var cache = cacheBuilder;

var kafkaBuilder = builder.AddKafka("kafka");
if (kafkaUiEnabled)
{
    kafkaBuilder.WithKafkaUI();
}
var kafka = kafkaBuilder;

var referenceService = builder.AddProject<Projects.AspirePoc_ReferenceService>("reference-service");

var app1 = builder.AddProject<Projects.AspirePoc_App1>("app1")
    .WithReference(cache)
    .WithReference(kafka)
    .WithReference(referenceService)
    .WaitFor(cache)
    .WaitFor(kafka)
    .WaitFor(referenceService)
    .WithEnvironment("Kafka__Topic", topic);

var app2 = builder.AddProject<Projects.AspirePoc_App2>("app2")
    .WithReference(kafka)
    .WaitFor(kafka)
    .WithEnvironment("Kafka__Topic", topic)
    .WithEnvironment("Kafka__ConsumerGroup", consumerGroup);

if (!string.IsNullOrEmpty(outputPath))
{
    app2.WithEnvironment("Output__Path", outputPath);
}

if (opensearchEnabled)
{
    var opensearch = builder.AddContainer("opensearch", "opensearchproject/opensearch", "2.16.0")
        .WithEnvironment("discovery.type", "single-node")
        .WithEnvironment("DISABLE_SECURITY_PLUGIN", "true")
        .WithEnvironment("DISABLE_INSTALL_DEMO_CONFIG", "true")
        .WithEnvironment("OPENSEARCH_JAVA_OPTS", "-Xms512m -Xmx512m")
        .WithHttpEndpoint(port: 9200, targetPort: 9200, name: "http");

    if (opensearchDashboardsEnabled)
    {
        builder.AddContainer("opensearch-dashboards", "opensearchproject/opensearch-dashboards", "2.16.0")
            .WithEnvironment("OPENSEARCH_HOSTS", "http://opensearch:9200")
            .WithEnvironment("DISABLE_SECURITY_DASHBOARDS_PLUGIN", "true")
            .WithHttpEndpoint(port: 5601, targetPort: 5601, name: "http")
            .WaitFor(opensearch);
    }

    builder.AddProject<Projects.AspirePoc_Indexer>("indexer")
        .WithReference(kafka)
        .WaitFor(kafka)
        .WaitFor(opensearch)
        .WithEnvironment("Kafka__Topic", topic)
        .WithEnvironment("Kafka__ConsumerGroup", "indexer-consumer-group")
        .WithEnvironment("ConnectionStrings__opensearch", "http://localhost:9200");
}

if (producerEnabled)
{
    builder.AddProject<Projects.AspirePoc_Producer>("producer")
        .WithReference(app1)
        .WaitFor(app1);
}

builder.Build().Run();

static bool ReadFlag(IConfiguration config, string key, bool @default = true)
{
    var value = config[key];
    if (string.IsNullOrEmpty(value)) return @default;
    return !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
}
