using DLCS.Core.Collections;
using DLCS.Repository;
using MediatR;

namespace API.Features.Customer.Requests;

/// <summary>
/// Delete specified API key.
/// Call will fail if current user is admin and there's only 1 key  
/// </summary>
public class DeleteApiKey : IRequest<DeleteApiKeyResult>
{
    public DeleteApiKey(int customerId, string key)
    {
        CustomerId = customerId;
        Key = key;
    }

    public int CustomerId { get; }
    public string Key { get; }
}

public class DeleteApiKeyResult
{
    public string Error { get; set; }
}

public class DeleteApiKeyHandler : IRequestHandler<DeleteApiKey, DeleteApiKeyResult>
{
    private readonly DlcsContext dbContext;

    public DeleteApiKeyHandler(
        DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<DeleteApiKeyResult> Handle(DeleteApiKey request, CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers.FindAsync(request.CustomerId);
        if (customer == null)
        {
            return new DeleteApiKeyResult { Error = "No Customer" };
        }

        if (customer.Keys.Contains(request.Key))
        {
            if (customer.Administrator && customer.Keys.Length == 1)
            {
                // We're not actually checking that it's _this_ key - but that's good!
                return new DeleteApiKeyResult
                {
                    Error = "Admin user cannot delete last key"
                };
            }

            customer.Keys = StringArrays.RemoveString(customer.Keys, request.Key);
            var i = await dbContext.SaveChangesAsync(cancellationToken);
            if (i == 1)
            {
                return new DeleteApiKeyResult();
            }

            return new DeleteApiKeyResult { Error = "Unable to delete key" };
        }

        return new DeleteApiKeyResult(); // nothing happened, but be silent as to why.
    }
}