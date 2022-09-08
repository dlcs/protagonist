using System.Threading;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Repository;
using MediatR;
using DlcsCustomer = DLCS.Model.Customers.Customer;

namespace API.Features.Customer.Requests;

/// <summary>
/// Make a partial update to a customer.
/// </summary>
/// <remarks>This only takes a single field as it's the only one that can be updated</remarks>
public class PatchCustomer : IRequest<PatchCustomerResult>
{
    public int CustomerId { get; }
    
    public string DisplayName { get; }

    public PatchCustomer(int customerId, string displayName)
    {
        CustomerId = customerId;
        DisplayName = displayName;
    }
}

public class PatchCustomerResult
{
    public UpdateResult UpdateResult { get; private init;}
    public DlcsCustomer? Customer { get; private init;}
    public string? Error { get; private init; }

    public static PatchCustomerResult Failure(string error, UpdateResult result = UpdateResult.Unknown)
        => new() { Error = error, UpdateResult = result };
    
    public static PatchCustomerResult Success(DlcsCustomer customer, UpdateResult result = UpdateResult.Updated)
        => new() { Customer = customer, UpdateResult = result };
}

public class PatchCustomerHandler : IRequestHandler<PatchCustomer, PatchCustomerResult>
{
    private readonly DlcsContext dlcsContext;

    public PatchCustomerHandler(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }
    
    public async Task<PatchCustomerResult> Handle(PatchCustomer request, CancellationToken cancellationToken)
    {
        var customer = await dlcsContext.Customers.FindAsync(new object?[] { request.CustomerId }, cancellationToken);
        if (customer == null)
        {
            return PatchCustomerResult.Failure("Customer not found", UpdateResult.NotFound);
        }

        // This is the only field that can be updated for an existing customer
        customer.DisplayName = request.DisplayName;

        var rowCount = await dlcsContext.SaveChangesAsync(cancellationToken);
        if (rowCount == 0)
        {
            return PatchCustomerResult.Failure("Unable to Patch Customer", UpdateResult.Error);
        }

        await dlcsContext.Entry(customer).ReloadAsync(cancellationToken);
        return PatchCustomerResult.Success(customer);
    }
}