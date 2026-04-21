namespace AspirePoc.App2;

public record EnrichedTransaction(
    string TransactionId,
    string CustomerId,
    string CustomerName,
    string CustomerTier,
    decimal DiscountRate,
    decimal Amount,
    string Currency);
