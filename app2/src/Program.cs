using AspirePoc.App2;
using Confluent.Kafka;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSingleton<IConsumer<string, string>>(sp =>
{
    var config = new ConsumerConfig
    {
        BootstrapServers = builder.Configuration["Kafka:BootstrapServers"],
        GroupId = builder.Configuration["Kafka:ConsumerGroup"] ?? "app2-consumer-group",
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = true
    };
    return new ConsumerBuilder<string, string>(config).Build();
});

builder.Services.AddSingleton<TransactionProcessor>();
builder.Services.AddHostedService<KafkaConsumerService>();

var app = builder.Build();

var bootstrapServers = app.Configuration["Kafka:BootstrapServers"]
    ?? throw new InvalidOperationException("Kafka:BootstrapServers is not configured");
var topic = app.Configuration["Kafka:Topic"] ?? KafkaConsumerService.DefaultTopic;
await KafkaTopicEnsurer.EnsureAsync(bootstrapServers, topic, app.Logger);

app.MapDefaultEndpoints();

app.MapGet("/hello", () => "app2 is alive");

app.Run();
