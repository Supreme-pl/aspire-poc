using System.Net.Http.Json;
using System.Text.Json;

namespace AspirePoc.Producer;

public sealed class ProducerService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient http;
    private readonly BatchGenerator generator;
    private readonly ILogger<ProducerService> logger;
    private readonly TimeSpan interval;
    private readonly int batchSize;

    public ProducerService(
        IHttpClientFactory httpClientFactory,
        BatchGenerator generator,
        IConfiguration config,
        ILogger<ProducerService> logger)
    {
        this.http = httpClientFactory.CreateClient(Constants.App1ClientName);
        this.generator = generator;
        this.logger = logger;
        this.interval = TimeSpan.FromSeconds(config.GetValue(Constants.IntervalConfigKey, 10));
        this.batchSize = config.GetValue(Constants.BatchSizeConfigKey, 5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Producer starting: interval={Interval}, batchSize={BatchSize}",
            interval, batchSize);

        try
        {
            await SendBatchAsync(stoppingToken);

            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SendBatchAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SendBatchAsync(CancellationToken ct)
    {
        var batch = generator.Generate(batchSize);

        var response = await http.PostAsJsonAsync(Constants.ProcessEndpoint, batch, JsonOptions, ct);

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation(
                "Posted batch {BatchId} with {Count} transactions",
                batch.BatchId, batch.Transactions.Count);
        }
        else
        {
            logger.LogWarning(
                "app1 returned {Status} for batch {BatchId}",
                response.StatusCode, batch.BatchId);
        }
    }
}
