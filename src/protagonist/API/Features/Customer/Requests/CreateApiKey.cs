using API.Settings;
using DLCS.Core.Collections;
using DLCS.Core.Encryption;
using DLCS.Repository;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Features.Customer.Requests;

/// <summary>
/// Create a new API key for customer
/// </summary>
public class CreateApiKey : IRequest<CreateApiKeyResult>
{
    public CreateApiKey(int customerId) => CustomerId = customerId;
    
    public int CustomerId { get; }
}

public class CreateApiKeyResult
{
    public string Key { get; set; }
    public string Secret { get; set; }
    public string Error { get; set; }
}


public class CreateApiKeyHandler : IRequestHandler<CreateApiKey, CreateApiKeyResult>
{
    private readonly DlcsContext dbContext;
    private readonly ILogger<CreateApiKeyHandler> logger;
    private readonly IEncryption encryption;
    private readonly ApiSettings settings;
    
    public CreateApiKeyHandler(
        DlcsContext dbContext,
        ILogger<CreateApiKeyHandler> logger,
        IEncryption encryption,
        IOptions<ApiSettings> options)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.encryption = encryption;
        settings = options.Value;
    }
    
    public async Task<CreateApiKeyResult> Handle(CreateApiKey request, CancellationToken cancellationToken)
    {
        var result = new CreateApiKeyResult();
        var customer = await dbContext.Customers.FindAsync(request.CustomerId);
        if (customer == null || settings.Salt.IsNullOrEmpty())
        {
            result.Error = "Insufficient data to create key";
            return result;
        }
        
        string apiKey = Guid.NewGuid().ToString();
        string apiSecret = encryption.Encrypt(String.Concat(settings.Salt, customer.Id.ToString(), apiKey));

        customer.Keys = StringArrays.EnsureString(customer.Keys, apiKey);
        var i = await dbContext.SaveChangesAsync(cancellationToken);
        if (i == 1)
        {
            result.Key = apiKey;
            result.Secret = apiSecret;
        }
        else
        {
            result.Error = "Unable to save new ApiKey";
        }

        return result;
    }
}