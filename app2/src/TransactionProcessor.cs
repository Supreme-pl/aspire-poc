using System.Globalization;

namespace AspirePoc.App2;

public sealed class TransactionProcessor
{
    private const string CsvHeader =
        "transactionId,customerId,customerName,customerTier,amount,discountRate,finalAmount,currency";

    private readonly string outputPath;
    private readonly ILogger<TransactionProcessor> logger;

    public TransactionProcessor(IConfiguration config, ILogger<TransactionProcessor> logger)
    {
        this.outputPath = ResolveOutputPath(config);
        this.logger = logger;

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
            logger.LogInformation("Cleared existing transaction output file {Path}", outputPath);
        }

        logger.LogInformation("Transaction output file: {Path}", outputPath);
    }

    public async Task ProcessAsync(EnrichedTransaction transaction, CancellationToken ct)
    {
        var original = new Money(transaction.Amount, transaction.Currency);
        var discounted = original.ApplyDiscount(transaction.DiscountRate);

        var isNewFile = !File.Exists(outputPath);
        var row = FormatRow(transaction, discounted);
        var content = isNewFile
            ? CsvHeader + Environment.NewLine + row + Environment.NewLine
            : row + Environment.NewLine;

        await File.AppendAllTextAsync(outputPath, content, ct);
    }

    private static string FormatRow(EnrichedTransaction t, Money discounted) =>
        string.Join(",",
            t.TransactionId,
            t.CustomerId,
            Escape(t.CustomerName),
            t.CustomerTier,
            t.Amount.ToString("F2", CultureInfo.InvariantCulture),
            t.DiscountRate.ToString(CultureInfo.InvariantCulture),
            discounted.Amount.ToString("F2", CultureInfo.InvariantCulture),
            t.Currency);

    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static string ResolveOutputPath(IConfiguration config) =>
        config["Output:Path"] is { Length: > 0 } configured
            ? configured
            : Path.Combine(Path.GetTempPath(), "aspire-poc", "transactions.csv");
}
