using System.Collections.Generic;
using API.Exceptions;
using API.Features.NamedQueries.Converters;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.NamedQueries.Requests; 

public class CreateOrUpdateNamedQuery : IRequest<ModifyEntityResult<NamedQuery>>
{
    public int CustomerId { get; }
    
    public NamedQuery NamedQuery { get; }

    public bool UpdateExisting { get; }
    
    public CreateOrUpdateNamedQuery(int customerId, NamedQuery namedQuery, string httpMethod)
    {
        CustomerId = customerId;
        NamedQuery = namedQuery;
        UpdateExisting = httpMethod == "PUT";
    }
}

public class CreateOrUpdateNamedQueryHandler : IRequestHandler<CreateOrUpdateNamedQuery, ModifyEntityResult<NamedQuery>>
{  
    private readonly DlcsContext dbContext;

    public CreateOrUpdateNamedQueryHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<ModifyEntityResult<NamedQuery>> Handle(CreateOrUpdateNamedQuery request, CancellationToken cancellationToken)
    {
        //TODO: Validate request body against a NamedQueryValidator
        var existingNamedQuery = await dbContext.NamedQueries.SingleOrDefaultAsync(
            nq => nq.Id == request.NamedQuery.Id, cancellationToken);
        if (existingNamedQuery == null)
        {
            await dbContext.NamedQueries.AddAsync(request.NamedQuery, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken); 
            return ModifyEntityResult<NamedQuery>.Success(request.NamedQuery, WriteResult.Created);
        }
        if (!request.UpdateExisting || existingNamedQuery.Customer != request.CustomerId )
        {
            return ModifyEntityResult<NamedQuery>.Failure("A named query with that ID already exists",
                WriteResult.Conflict);
        }
        existingNamedQuery.Template = request.NamedQuery.Template;
        await dbContext.SaveChangesAsync(cancellationToken); 
        return ModifyEntityResult<NamedQuery>.Success(request.NamedQuery);
    }
}