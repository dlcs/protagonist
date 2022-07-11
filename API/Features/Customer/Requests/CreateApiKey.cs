using System.Threading;
using System.Threading.Tasks;
using DLCS.Repository;
using MediatR;
using Microsoft.Extensions.Logging;

namespace API.Features.Customer.Requests;


public class CreateApiKey : IRequest<CreateApiKeyResult>
{
    public CreateApiKey(int customerId) => CustomerId = customerId;
    public int CustomerId { get; }
}

public class CreateApiKeyResult
{
    public string Key { get; set; }
    public string Secret { get; set; }
}


public class CreateApiKeyHandler : IRequestHandler<CreateApiKey, CreateApiKeyResult>
{
    private readonly DlcsContext dbContext;
    private readonly ILogger<CreateApiKeyHandler> logger;
    
    public CreateApiKeyHandler(
        DlcsContext dbContext,
        ILogger<CreateApiKeyHandler> logger)
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }
    
    public Task<CreateApiKeyResult> Handle(CreateApiKey request, CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }
}