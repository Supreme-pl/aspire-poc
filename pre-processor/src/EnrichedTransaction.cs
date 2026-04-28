namespace AspirePoc.PreProcessor;

public record EnrichedTransaction(
    string TransactionId,
    string CustomerId,
    string CustomerName,
    string CustomerTier,
    decimal DiscountRate,
    decimal Amount,
    string Currency);
