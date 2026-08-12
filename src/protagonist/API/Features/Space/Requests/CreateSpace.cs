using API.Exceptions;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Customers;
using DLCS.Model.Spaces;
using MediatR;

namespace API.Features.Space.Requests;

/// <remark>
/// Create a new space for customer
/// </remark>
public class CreateSpace(int customer, string name) : IRequest<ModifyEntityResult<DLCS.Model.Spaces.Space>>
{
    public string Name { get; } = name;
    public int Customer { get; } = customer;
    public string? ImageBucket { get; set; }
    public string[]? Tags { get; set; }
    public string[]? Roles { get; set; }
}

public class CreateSpaceHandler(
    ISpaceRepository spaceRepository,
    ICustomerRepository customerRepository)
    : IRequestHandler<CreateSpace, ModifyEntityResult<DLCS.Model.Spaces.Space>>
{
    public async Task<ModifyEntityResult<DLCS.Model.Spaces.Space>> Handle(CreateSpace request, CancellationToken cancellationToken)
    {
        await ValidateRequest(request);
       
        var existing = await spaceRepository.GetSpace(request.Customer, request.Name, cancellationToken);
        if (existing != null)
        {
            return ModifyEntityResult<DLCS.Model.Spaces.Space>.Failure("A space with this name already exists.",
                WriteResult.Conflict);
        }

        var newSpace = await spaceRepository.CreateSpace(request.Customer, request.Name, request.ImageBucket,
            request.Tags, request.Roles, cancellationToken);

        return ModifyEntityResult<DLCS.Model.Spaces.Space>.Success(newSpace, WriteResult.Created);
    }
    
    private async Task ValidateRequest(CreateSpace request)
    {
        var customer = await customerRepository.GetCustomer(request.Customer);
        if (customer == null)
        { 
            throw new BadRequestException("Space must be created for an existing Customer.");
        }
    }
}
