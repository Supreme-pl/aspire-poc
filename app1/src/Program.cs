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
        AllowAutoCreateTopics = true,
        MessageTimeoutMs = 15000,
        RequestTimeoutMs = 10000,
        MessageSendMaxRetries = 3
    };
    return new ProducerBuilder<string, string>(config)
        .SetLogHandler((_, _) => { })
        .Build();
});

builder.Services.AddSingleton<CustomerLookup>();
builder.Services.AddSingleton<EnrichmentHandler>();

var app = builder.Build();

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
