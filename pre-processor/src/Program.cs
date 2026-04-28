using AspirePoc.PreProcessor;
using Confluent.Kafka;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisClient(Constants.CacheConnectionName);

builder.Services.AddHttpClient(Constants.AmpClientName, client =>
{
    client.BaseAddress = new Uri("http://amp");
});

builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = builder.Configuration.GetConnectionString("kafka"),
        AllowAutoCreateTopics = true,
        MessageTimeoutMs = 15000,
        RequestTimeoutMs = 10000,
        MessageSendMaxRetries = 3
    };
    return new ProducerBuilder<string, string>(config)
        .SetLogHandler((_, _) => { })
        .Build();
});

builder.Services.AddSingleton<ICustomerLookup, CustomerLookup>();
builder.Services.AddSingleton<EnrichmentHandler>();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/hello", () => "pre-processor is alive");

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
