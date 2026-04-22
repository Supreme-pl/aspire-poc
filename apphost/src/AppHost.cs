var builder = DistributedApplication.CreateBuilder(args);

var topic = builder.Configuration["Kafka:Topic"] ?? "transactions.enriched";
var consumerGroup = builder.Configuration["Kafka:ConsumerGroup"] ?? "app2-consumer-group";
var outputPath = builder.Configuration["Output:Path"];
var producerEnabled = !string.Equals(builder.Configuration["Producer:Enabled"], "false", StringComparison.OrdinalIgnoreCase);
var kafkaUiEnabled = !string.Equals(builder.Configuration["KafkaUI:Enabled"], "false", StringComparison.OrdinalIgnoreCase);
var redisInsightEnabled = !string.Equals(builder.Configuration["RedisInsight:Enabled"], "false", StringComparison.OrdinalIgnoreCase);

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

if (producerEnabled)
{
    builder.AddProject<Projects.AspirePoc_Producer>("producer")
        .WithReference(app1)
        .WaitFor(app1);
}

builder.Build().Run();
