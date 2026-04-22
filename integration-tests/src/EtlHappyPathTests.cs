using System.Net.Http.Json;

namespace AspirePoc.IntegrationTests;

public class EtlHappyPathTests
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan ProcessingTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Batch_IsEnrichedAndWrittenToCsv_WithDiscountApplied()
    {
        var ct = CancellationToken.None;

        var runId = Guid.NewGuid().ToString("N")[..8];
        var tempDir = Path.Combine(Path.GetTempPath(), $"aspire-poc-test-{runId}");
        Directory.CreateDirectory(tempDir);
        var outputPath = Path.Combine(tempDir, "transactions.csv");

        var hostArgs = new[]
        {
            $"--Kafka:Topic=test-topic-{runId}",
            $"--Kafka:ConsumerGroup=test-group-{runId}",
            $"--Output:Path={outputPath}",
            "--Producer:Enabled=false",
            "--Console:Enabled=false"
        };

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AspirePoc_AppHost>(hostArgs, ct);

        await using var app = await appHost.BuildAsync(ct).WaitAsync(StartupTimeout, ct);
        await app.StartAsync(ct).WaitAsync(StartupTimeout, ct);

        await app.ResourceNotifications.WaitForResourceHealthyAsync("app1", ct).WaitAsync(StartupTimeout, ct);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("app2", ct).WaitAsync(StartupTimeout, ct);

        using var httpClient = app.CreateHttpClient("app1");

        var batch = new
        {
            batchId = $"B-test-{runId}",
            transactions = new object[]
            {
                new { transactionId = "T-001", customerId = "C-100", amount = 100.00m, currency = "USD" },
                new { transactionId = "T-002", customerId = "C-101", amount = 200.00m, currency = "GBP" }
            }
        };

        using var response = await httpClient.PostAsJsonAsync("/process", batch, ct);
        response.EnsureSuccessStatusCode();

        var lines = await PollForCsvLines(outputPath, expected: 3, ProcessingTimeout, ct);

        Assert.Equal(3, lines.Length);
        Assert.StartsWith("transactionId,customerId,", lines[0]);
        Assert.Contains("T-001,C-100,ACME Corp,premium,100.00,0.15,85.00,USD", lines);
        Assert.Contains("T-002,C-101,Globex Ltd,standard,200.00,0.05,190.00,GBP", lines);
    }

    private static async Task<string[]> PollForCsvLines(
        string path,
        int expected,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path))
            {
                var lines = await File.ReadAllLinesAsync(path, ct);
                if (lines.Length >= expected)
                {
                    return lines;
                }
            }
            await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        }

        var actualCount = File.Exists(path) ? (await File.ReadAllLinesAsync(path, ct)).Length : 0;
        throw new TimeoutException(
            $"Expected {expected} lines in {path}, got {actualCount} within {timeout.TotalSeconds}s");
    }
}
