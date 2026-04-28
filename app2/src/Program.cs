using AspirePoc.App2;
using Confluent.Kafka;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSingleton<IConsumer<string, string>>(sp =>
{
    var config = new ConsumerConfig
    {
        BootstrapServers = builder.Configuration.GetConnectionString(Constants.KafkaConnectionName),
        GroupId = builder.Configuration[Constants.KafkaConsumerGroupConfigKey] ?? Constants.DefaultConsumerGroup,
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = true,
        TopicMetadataRefreshIntervalMs = 1000
    };
    return new ConsumerBuilder<string, string>(config)
        .SetLogHandler((_, _) => { })
        .Build();
});

builder.Services.AddSingleton<ITransactionSink, CsvTransactionSink>();
builder.Services.AddHostedService<KafkaConsumerService>();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/hello", () => "app2 is alive");

CsvTransactionSink.ClearOutputFileForFreshRun(
    app.Configuration,
    app.Services.GetRequiredService<ILogger<CsvTransactionSink>>());

app.Run();
