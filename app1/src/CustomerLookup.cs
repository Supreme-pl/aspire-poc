using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StackExchange.Redis;

namespace AspirePoc.App1;

public sealed class CustomerLookup : ICustomerLookup
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer redis;
    private readonly HttpClient referenceClient;
    private readonly ILogger<CustomerLookup> logger;

    public CustomerLookup(
        IConnectionMultiplexer redis,
        IHttpClientFactory httpClientFactory,
        ILogger<CustomerLookup> logger)
    {
        this.redis = redis;
        this.referenceClient = httpClientFactory.CreateClient(Constants.ReferenceServiceClientName);
        this.logger = logger;
    }

    public async Task<Customer?> GetAsync(string customerId, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var key = CacheKey(customerId);

        var cached = await db.StringGetAsync(key);
        if (cached.HasValue)
        {
            logger.LogDebug("Cache hit for {CustomerId}", customerId);
            return JsonSerializer.Deserialize<Customer>((string)cached!, JsonOptions);
        }

        logger.LogDebug("Cache miss for {CustomerId}", customerId);
        var response = await referenceClient.GetAsync($"/customers/{customerId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();

        var customer = await response.Content.ReadFromJsonAsync<Customer>(JsonOptions, ct)
            ?? throw new InvalidOperationException($"Reference service returned empty body for {customerId}");

        await db.StringSetAsync(key, JsonSerializer.Serialize(customer, JsonOptions));
        return customer;
    }

    private static string CacheKey(string customerId) => $"{Constants.CustomerCacheKeyPrefix}{customerId}";
}
