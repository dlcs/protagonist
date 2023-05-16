using API.Auth;
using DLCS.Core.Collections;
using DLCS.Repository;
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
    
    public CreateApiKeyHandler(
        DlcsContext dbContext,
        ApiKeyGenerator apiKeyGenerator)
    {
        this.dbContext = dbContext;
        this.apiKeyGenerator = apiKeyGenerator;
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
            return CreateApiKeyResult.Success(apiKey, apiSecret);
        }

        return CreateApiKeyResult.Fail("Unable to save new ApiKey");
    }
}