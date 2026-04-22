using AspirePoc.App1;
using Confluent.Kafka;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisClient("cache");

builder.Services.AddHttpClient("reference-service", client =>
{
    client.BaseAddress = new Uri("http://reference-service");
});

builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = builder.Configuration["Kafka:BootstrapServers"],
        AllowAutoCreateTopics = true
    };
    return new ProducerBuilder<string, string>(config)
        .SetLogHandler((_, _) => { })
        .Build();
});

builder.Services.AddSingleton<CustomerLookup>();
builder.Services.AddSingleton<EnrichmentHandler>();

var app = builder.Build();

var bootstrapServers = app.Configuration["Kafka:BootstrapServers"]
    ?? throw new InvalidOperationException("Kafka:BootstrapServers is not configured");
var topic = app.Configuration["Kafka:Topic"] ?? EnrichmentHandler.DefaultTopic;
await KafkaTopicEnsurer.EnsureAsync(bootstrapServers, topic, app.Logger);

app.MapDefaultEndpoints();

app.MapGet("/hello", () => "app1 is alive");

app.MapPost("/process", async (Batch batch, EnrichmentHandler handler, CancellationToken ct) =>
{
    var published = await handler.ProcessAsync(batch, ct);
    return Results.Ok(new
    {
        batch.BatchId,
        transactionsReceived = batch.Transactions.Count,
        eventsPublished = published
    });
});

app.Run();
