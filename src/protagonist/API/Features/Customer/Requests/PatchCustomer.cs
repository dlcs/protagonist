using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Repository;
using MediatR;
using DlcsCustomer = DLCS.Model.Customers.Customer;

namespace API.Features.Customer.Requests;

/// <summary>
/// Make a partial update to a customer.
/// </summary>
/// <remarks>This only takes a single field as it's the only one that can be updated</remarks>
public class PatchCustomer : IRequest<ModifyEntityResult<DlcsCustomer>>
{
    public int CustomerId { get; }
    
    public string DisplayName { get; }

    public PatchCustomer(int customerId, string displayName)
    {
        CustomerId = customerId;
        DisplayName = displayName;
    }
}

public class PatchCustomerHandler : IRequestHandler<PatchCustomer, ModifyEntityResult<DlcsCustomer>>
{
    private readonly DlcsContext dlcsContext;

    public PatchCustomerHandler(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }
    
    public async Task<ModifyEntityResult<DlcsCustomer>> Handle(PatchCustomer request, CancellationToken cancellationToken)
    {
        var customer = await dlcsContext.Customers.FindAsync(new object?[] { request.CustomerId }, cancellationToken);
        if (customer == null)
        {
            return ModifyEntityResult<DlcsCustomer>.Failure("Customer not found", WriteResult.NotFound);
        }

        // This is the only field that can be updated for an existing customer
        customer.DisplayName = request.DisplayName;

        var rowCount = await dlcsContext.SaveChangesAsync(cancellationToken);
        if (rowCount == 0)
        {
            return ModifyEntityResult<DlcsCustomer>.Failure("Unable to Patch Customer", WriteResult.Error);
        }

        await dlcsContext.Entry(customer).ReloadAsync(cancellationToken);
        return ModifyEntityResult<DlcsCustomer>.Success(customer);
    }
}