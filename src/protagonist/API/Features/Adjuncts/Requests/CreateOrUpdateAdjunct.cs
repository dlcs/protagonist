using API.Infrastructure;
using API.Infrastructure.Messaging.General;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
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

public class CreateOrUpdateAdjunctHandler(DlcsContext dbContext, IIngestNotificationSender  notificationSender, IDeliverableNotificationSender deliverableNotificationSender)
    : IRequestHandler<CreateOrUpdateAdjunct, ModifyEntityResult<Adjunct>>
{
    public async Task<ModifyEntityResult<Adjunct>> Handle(CreateOrUpdateAdjunct request, CancellationToken cancellationToken)
    {
        var adjunct = request.Adjunct;
        var isCreate = true;

        // We can determine that immediately, remember for multiple uses below
        var toBeIngested = adjunct.IsToBeIngested();
        
        Adjunct? dbAdjunct = null;
        Adjunct? existingAdjunct = null;
        
        if (!request.CreateOnly)
        {
            dbAdjunct = await dbContext.Adjuncts.SingleOrDefaultAsync(a =>
                a.Id == adjunct.Id && a.AssetId == adjunct.AssetId, cancellationToken);
        }

        if (dbAdjunct != null)
        {
            existingAdjunct = dbAdjunct.Clone();
            
            // existing is not null => it is not create scenario
            isCreate = false;

            if (!toBeIngested)
            {
                // This is external adjunct, and the size is irrelevant for size calculations,
                // as this adjunct will not hit Engine - we copy whatever was submitted

                dbAdjunct.Size = adjunct.Size;
            }
            else if(!dbAdjunct.IsToBeIngested())
            {
                // was external, now is hosted
                
                // For hosted (ingested) adjuncts we let Engine handle this property
                // as it becomes relevant to storage limits. However, if the pre-existing
                // adjunct was EXTERNAL, the size doesn't count toward those limits.

                // To ensure correct calculations in the engine, we will set Size to null.
                // This will allow Engine to increase the total adjunct size by the size
                // of new version of the adjunct, regardless what size the external one had.

                dbAdjunct.Size = null;
            }
            
            dbAdjunct.MediaType = adjunct.MediaType;
            dbAdjunct.IIIFLink = adjunct.IIIFLink;
            dbAdjunct.Profile = adjunct.Profile;
            dbAdjunct.Label = adjunct.Label;
            dbAdjunct.Language = adjunct.Language;
            dbAdjunct.ExternalId = adjunct.ExternalId;
            dbAdjunct.Origin = adjunct.Origin;
            dbAdjunct.Error = adjunct.Error;
            dbAdjunct.Type = adjunct.Type;
            dbAdjunct.Motivation =  adjunct.Motivation;
            dbAdjunct.Ingesting = adjunct.Ingesting;
        }
        else
        {
            dbAdjunct = adjunct;
            dbAdjunct.Created = DateTime.UtcNow;

            if (toBeIngested)
            {
                // Will be set by the Engine, disregard any submitted value
                // See comments above for more details
                dbAdjunct.Size = null; 
            }
            // else it's external, and we don't care about the Size property in the context of processing - leave as is 
            
            await dbContext.Adjuncts.AddAsync(dbAdjunct, cancellationToken);
        }

        if (!toBeIngested)
        {
            // It is either creation of new external, or updating external->external, or updating hosted->external
            // In those cases we don't send to Engine for ingestion and finalizing is done in-API, so we set now()
            dbAdjunct.Finished = DateTime.UtcNow;
            
            // otherwise we leave it as either `null` for create or existing as "last finished" - in both cases
            // Engine will set the property when done ingesting
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
        
        dbAdjunct = dbContext.Adjuncts.Include(a=>a.Asset)
            .Single(a=>a.Id == dbAdjunct.Id && a.AssetId == dbAdjunct.AssetId);

        if (toBeIngested)
        {
            var success = await notificationSender.SendIngestAdjunctRequest(dbAdjunct, cancellationToken);
            if (!success)
            {
                return ModifyEntityResult<Adjunct>.Failure(
                    $"Adjunct with id '{adjunct.Id}' failed submission for ingestion and will need to be resubmitted",
                    WriteResult.NotFound);
            }
        }

        var adjunctModificationRecord = isCreate
            ? NotificationRecord<Adjunct>.Create(dbAdjunct)
            : NotificationRecord<Adjunct>.Update(existingAdjunct!, dbAdjunct, toBeIngested);

        await deliverableNotificationSender.SendDeliverableModifiedMessage(adjunctModificationRecord, cancellationToken);

        return ModifyEntityResult<Adjunct>.Success(dbAdjunct,
            isCreate ? WriteResult.Created : WriteResult.Updated);
    }
}
