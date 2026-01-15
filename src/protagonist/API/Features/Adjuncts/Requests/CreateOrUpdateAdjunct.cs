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
    public Adjunct Adjunct { get; } = adjunct;
    
    /// <summary>
    /// Whether only creation is allowed (no update)
    /// </summary>
    public bool CreateOnly { get; } = createOnly;
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
            dbAdjunct.MediaType = adjunct.MediaType;
            dbAdjunct.IIIFLink = adjunct.IIIFLink;
            dbAdjunct.Profile = adjunct.Profile;
            dbAdjunct.Label = adjunct.Label;
            dbAdjunct.Language = adjunct.Language;
            dbAdjunct.ExternalId = adjunct.ExternalId;
            dbAdjunct.Finished = DateTime.UtcNow;
            dbAdjunct.Size = adjunct.Size;
        }
        else
        {
            adjunct.Finished = DateTime.UtcNow;
            await dbContext.Adjuncts.AddAsync(adjunct, cancellationToken);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            var databaseError = ex.GetDatabaseError();
            return databaseError switch
            {
                UniqueConstraintError => ModifyEntityResult<Adjunct>.Failure(
                    $"Create failed. An adjunct with id '{adjunct.Id}' already exists", WriteResult.Conflict),
                DbForeignKeyConstraintError => ModifyEntityResult<Adjunct>.Failure($"Asset with id '{adjunct.AssetId}' not found",
                    WriteResult.NotFound),
                _ => ModifyEntityResult<Adjunct>.Failure($"Unknown database error saving adjunct '{adjunct.AssetId}'")
            };
        }

        return ModifyEntityResult<Adjunct>.Success(dbAdjunct ?? adjunct,
            dbAdjunct == null ? WriteResult.Created : WriteResult.Updated);
    }
}
