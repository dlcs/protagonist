using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Assets;
using DLCS.Repository;
using DLCS.Repository.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Adjuncts.Requests;

public class CreateOrUpdateAdjunct(Adjunct adjunct, bool createOnly) : IRequest<ModifyEntityResult<Adjunct>>
{
    /// <summary>
    /// The adjunct to create/update
    /// </summary>
    public Adjunct Adjunct { get; set; } = adjunct;
    
    /// <summary>
    /// Whether only creation is allowed (no update)
    /// </summary>
    public bool CreateOnly { get; set; } = createOnly;
}

public class CreateOrUpdateAdjunctHandler(DlcsContext dbContext)
    : IRequestHandler<CreateOrUpdateAdjunct, ModifyEntityResult<Adjunct>>
{
    public async Task<ModifyEntityResult<Adjunct>> Handle(CreateOrUpdateAdjunct request, CancellationToken cancellationToken)
    {
        var adjunct = request.Adjunct;
        
        Adjunct? dbAdjunct = null;
        if (!request.CreateOnly)
        {
            dbAdjunct = await dbContext.Adjuncts.SingleOrDefaultAsync(a =>
                a.Id == adjunct.Id && a.AssetId == adjunct.AssetId, cancellationToken);
        }

        if (dbAdjunct != null)
        {
            dbAdjunct.Id = adjunct.Id;
            dbAdjunct.MediaType = adjunct.MediaType;
            dbAdjunct.IIIFLink = adjunct.IIIFLink;
            dbAdjunct.AssetId = adjunct.AssetId;
            dbAdjunct.Profile = adjunct.Profile;
            dbAdjunct.Label = adjunct.Label;
            dbAdjunct.Language = adjunct.Language;
            dbAdjunct.ExternalId = adjunct.ExternalId;
            dbAdjunct.Finished = DateTime.UtcNow;
            dbAdjunct.Size = adjunct.Size;
        }
        else
        {
            request.Adjunct.Created = DateTime.UtcNow;
            request.Adjunct.Finished = DateTime.UtcNow;
            
            await dbContext.Adjuncts.AddAsync(request.Adjunct, cancellationToken);
        }
        
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.GetDatabaseError() is UniqueConstraintError)
        {
            return ModifyEntityResult<Adjunct>.Failure(
                $"An adjunct called '{request.Adjunct.Id}' already exists",
                WriteResult.Conflict);
        }

        return ModifyEntityResult<Adjunct>.Success(dbAdjunct ?? request.Adjunct,
            dbAdjunct == null ? WriteResult.Created : WriteResult.Updated);
    }
}
