namespace AspirePoc.PreProcessor;

public interface ICustomerLookup
{
    Task<Customer?> GetAsync(string customerId, CancellationToken ct);
}
