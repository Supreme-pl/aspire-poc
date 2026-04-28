using AspirePoc.Indexer;
using Confluent.Kafka;
using OpenSearch.Client;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSingleton<IConsumer<string, string>>(sp =>
{
    var config = new ConsumerConfig
    {
        BootstrapServers = builder.Configuration.GetConnectionString(Constants.KafkaConnectionName),
        GroupId = builder.Configuration["Kafka:ConsumerGroup"] ?? Constants.DefaultConsumerGroup,
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = true,
        TopicMetadataRefreshIntervalMs = 1000
    };
    return new ConsumerBuilder<string, string>(config)
        .SetLogHandler((_, _) => { })
        .Build();
});

builder.Services.AddSingleton<IOpenSearchClient>(sp =>
{
    var endpoint = builder.Configuration.GetConnectionString(Constants.OpenSearchConnectionName)
        ?? throw new InvalidOperationException("OpenSearch connection string is not configured");
    var settings = new ConnectionSettings(new Uri(endpoint));
    return new OpenSearchClient(settings);
});

builder.Services.AddSingleton<OpenSearchIndexer>();
builder.Services.AddHostedService<KafkaConsumerService>();

var host = builder.Build();
host.Run();
