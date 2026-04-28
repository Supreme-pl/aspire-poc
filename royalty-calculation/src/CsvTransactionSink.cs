using System.Globalization;

namespace AspirePoc.RoyaltyCalculation;

public sealed class CsvTransactionSink : ITransactionSink
{
    private const string CsvHeader =
        "transactionId,customerId,customerName,customerTier,amount,discountRate,finalAmount,currency";

    private readonly string outputPath;

    public CsvTransactionSink(IConfiguration config, ILogger<CsvTransactionSink> logger)
    {
        this.outputPath = ResolveOutputPath(config);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        logger.LogInformation("Transaction output file: {Path}", outputPath);
    }

    public async Task WriteAsync(EnrichedTransaction transaction, Money discounted, CancellationToken ct)
    {
        var isNewFile = !File.Exists(outputPath);
        var row = FormatRow(transaction, discounted);
        var content = isNewFile
            ? CsvHeader + Environment.NewLine + row + Environment.NewLine
            : row + Environment.NewLine;

        await File.AppendAllTextAsync(outputPath, content, ct);
    }

    public static void ClearOutputFileForFreshRun(IConfiguration config, ILogger logger)
    {
        var path = ResolveOutputPath(config);
        if (File.Exists(path))
        {
            File.Delete(path);
            logger.LogInformation("Cleared existing transaction output file {Path}", path);
        }
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
        config[Constants.OutputPathConfigKey] is { Length: > 0 } configured
            ? configured
            : Path.Combine(Path.GetTempPath(), "aspire-poc", "transactions.csv");
}
