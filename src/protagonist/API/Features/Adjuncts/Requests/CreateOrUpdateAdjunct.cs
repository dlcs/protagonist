using System.Collections.Generic;
using API.Infrastructure;
using API.Infrastructure.Messaging.General;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Repository;
using DLCS.Repository.Exceptions;
using Hydra.Collections;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Features.Adjuncts.Requests;

public class CreateOrUpdateAdjunct(Adjunct[] adjuncts, bool createOnly) : IRequest<ModifyEntityResult<Adjunct[]>>
{
    /// <summary>
    /// The adjunct to create/update
    /// </summary>
    public Adjunct[] Adjuncts { get; } = adjuncts;

    /// <summary>
    /// Whether only creation is allowed (no update)
    /// </summary>
    public bool CreateOnly { get; } = createOnly;
}


public class CreateOrUpdateAdjunctHandler(
    DlcsContext dbContext,
    IIngestNotificationSender notificationSender,
    IDeliverableNotificationSender deliverableNotificationSender,
    ILogger<CreateOrUpdateAdjunctHandler> logger)
    : IRequestHandler<CreateOrUpdateAdjunct, ModifyEntityResult<Adjunct[]>>
{
    public async Task<ModifyEntityResult<Adjunct[]>> Handle(CreateOrUpdateAdjunct request,
        CancellationToken cancellationToken)
    {
        // this is set from path to the same value for all, simplifying querying:
        var assetId = request.Adjuncts[0].AssetId;
        var adjunctIds = request.Adjuncts.Select(a => a.Id).ToArray();
        var existing = request.CreateOnly
            ? [] // act as if no existing
            : await dbContext.Adjuncts
                .Where(a => a.AssetId == assetId && adjunctIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, cancellationToken);

        var anyUpdates = false;

        var adjuncts = new List<AdjunctDocument>(request.Adjuncts.Length);
        foreach (var adjunct in request.Adjuncts)
        {
            try
            {
                var existingAdjunct = !request.CreateOnly && existing.TryGetValue(adjunct.Id, out var e) ? e : null;
                anyUpdates =
                    anyUpdates ||
                    existingAdjunct != null; // true if at least one is updating existing - this or previous

                var processed = await HandleAdjunct(adjunct, existingAdjunct, cancellationToken);
                adjuncts.Add(processed);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Error processing {Identifier}", adjunct.Identifier());
                return ModifyEntityResult<Adjunct[]>.Failure(
                    $"Unknown database error saving '{adjunct.Identifier()}'");
            }
        }

        // Add/update of all in a list has been done successfully, but changes weren't saved yet 
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            var databaseError = ex.GetDatabaseError();
            return databaseError switch
            {
                UniqueConstraintError => ModifyEntityResult<Adjunct[]>.Failure(
                    $"Create failed. Adjunct or adjuncts with id(s) in ({string.Join(',', adjuncts.Select(a => a.Processed.Id))}) already exists",
                    WriteResult.Conflict),
                DbForeignKeyConstraintError => ModifyEntityResult<Adjunct[]>.Failure($"Asset with id '{assetId}' not found",
                    WriteResult.NotFound),
                _ => ModifyEntityResult<Adjunct[]>.Failure($"Unknown database error saving adjuncts for {assetId}")
            };
        }

        // Reload all from db - this confirms all saved fine, and we also retrieve the asset object for use below
        var currentAdjuncts = await dbContext.Adjuncts.AsNoTracking().Include(a => a.Asset)
            .Where(a => a.AssetId == assetId && adjunctIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken: cancellationToken);


        List<string> failed = [];

        foreach (var adjunct in adjuncts)
        {
            if (currentAdjuncts.TryGetValue(adjunct.Processed.Id, out var updated))
            {
                // this is a bit clunky, but we don't want to query individually for each - there could be a lot
                adjunct.Processed = updated;
            }

            var success = await TryIngestNotify(adjunct, cancellationToken);
            if (!success)
            {
                failed.Add(adjunct.Processed.Id);
            }
        }

        if (failed.Count != 0)
        {
            return ModifyEntityResult<Adjunct[]>.Failure(
                $"Adjuncts with ids '{string.Join(", ", failed)}' for asset {assetId} failed submission for ingestion and will need to be resubmitted",
                WriteResult.NotFound);
        }
        
        return ModifyEntityResult<Adjunct[]>.Success( adjuncts.Select(a => a.Processed).ToArray(),
            anyUpdates ? WriteResult.Updated : WriteResult.Created);
    }

    private async Task<bool> TryIngestNotify(AdjunctDocument adjunct, CancellationToken cancellationToken)
    {
        if (adjunct.ToBeIngested)
        {
            var success = await notificationSender.SendIngestAdjunctRequest(adjunct.Processed, cancellationToken);
            if (!success)
            {
                return false;
            }
        }


        var adjunctModificationRecord = adjunct.IsUpdate
            ? NotificationRecord<Adjunct>.Update(adjunct.Original!, adjunct.Processed, adjunct.ToBeIngested)
            : NotificationRecord<Adjunct>.Create(adjunct.Processed);

        await deliverableNotificationSender.SendDeliverableModifiedMessage(adjunctModificationRecord,
            cancellationToken);

        // all good
        return true;
    }

    private async Task<AdjunctDocument> HandleAdjunct(Adjunct adjunct, Adjunct? dbAdjunct,
        CancellationToken cancellationToken)
    {
        // We can determine that immediately, remember for multiple uses below
        var toBeIngested = adjunct.IsToBeIngested();
        Adjunct? existingAdjunct = null;


        if (dbAdjunct != null)
        {
            existingAdjunct = dbAdjunct.Clone();

            if (!toBeIngested)
            {
                // This is external adjunct, and the size is irrelevant for size calculations,
                // as this adjunct will not hit Engine - we copy whatever was submitted

                dbAdjunct.Size = adjunct.Size;
            }
            else if (!dbAdjunct.IsToBeIngested())
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
            dbAdjunct.Provides = adjunct.Provides;
            dbAdjunct.Motivation = adjunct.Motivation;
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

        return new AdjunctDocument(dbAdjunct, existingAdjunct);
    }
    
    private class AdjunctDocument(Adjunct adjunct, Adjunct? existingAdjunct)
    {
        public bool ToBeIngested { get; } = adjunct.IsToBeIngested();
        public bool IsUpdate => Original != null;
        public Adjunct? Original { get; } = existingAdjunct;
        public Adjunct Processed { get; set; } = adjunct;
    }
}
