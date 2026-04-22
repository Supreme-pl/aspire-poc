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
        EnableAutoCommit = true,
        TopicMetadataRefreshIntervalMs = 1000
    };
    return new ConsumerBuilder<string, string>(config)
        .SetLogHandler((_, _) => { })
        .Build();
});

builder.Services.AddSingleton<TransactionProcessor>();
builder.Services.AddHostedService<KafkaConsumerService>();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/hello", () => "app2 is alive");

app.Run();
