namespace AspirePoc.App1;

public interface ICustomerLookup
{
    Task<Customer?> GetAsync(string customerId, CancellationToken ct);
}
