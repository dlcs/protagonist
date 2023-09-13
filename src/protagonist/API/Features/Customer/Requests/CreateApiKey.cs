using API.Auth;
using DLCS.Core.Collections;
using DLCS.Repository;
using LazyCache;
using MediatR;

namespace API.Features.Customer.Requests;

/// <summary>
/// Create a new API key for customer
/// </summary>
public class CreateApiKey : IRequest<CreateApiKeyResult>
{
    public CreateApiKey(int customerId) => CustomerId = customerId;
    
    public int CustomerId { get; }
}

public class CreateApiKeyHandler : IRequestHandler<CreateApiKey, CreateApiKeyResult>
{
    private readonly DlcsContext dbContext;
    private readonly ApiKeyGenerator apiKeyGenerator;
    private readonly IAppCache appCache;
    
    public CreateApiKeyHandler(
        DlcsContext dbContext,
        ApiKeyGenerator apiKeyGenerator,
        IAppCache appCache)
    {
        this.dbContext = dbContext;
        this.apiKeyGenerator = apiKeyGenerator;
        this.appCache = appCache;
    }
    
    public async Task<CreateApiKeyResult> Handle(CreateApiKey request, CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers.FindAsync(new object?[] { request.CustomerId },
            cancellationToken: cancellationToken);
        if (customer == null)
        {
            return CreateApiKeyResult.Fail("Insufficient data to create key");
        }
        
        var (apiKey, apiSecret) = apiKeyGenerator.CreateApiKey(customer);
        
        customer.Keys = StringArrays.EnsureString(customer.Keys, apiKey);
        var i = await dbContext.SaveChangesAsync(cancellationToken);
        if (i == 1)
        {
            appCache.Remove(CacheKeys.Customer(request.CustomerId));
            return CreateApiKeyResult.Success(apiKey, apiSecret);
        }
        
        return CreateApiKeyResult.Fail("Unable to save new ApiKey");
    }
}