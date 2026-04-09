using System.Collections.Generic;
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
    AdjunctUpsertService adjunctUpsertService,
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
        
        // we gather the id's provided as they will be used throughout:
        var adjunctIds = request.Adjuncts.Select(a => a.Id).ToArray();
        
        // preload any of the adjuncts that already exist (= should be updated)
        // note that we "pretend" there are no existing ones if request is marked as "create only"
        var existing = request.CreateOnly
            ? [] // act as if no existing
            : await dbContext.Adjuncts
                .Where(a => a.AssetId == assetId && adjunctIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, cancellationToken);

        // trip-flag that will determine result type
        var anyUpdates = false;

        // We use a custom "wrapper" document for the Adjuncts being processed
        // This reduces the complexity of preserving set of data for each adjunct in some sort of dictionaries here
        var adjuncts = new List<AdjunctDocument>(request.Adjuncts.Length);
        foreach (var adjunct in request.Adjuncts)
        {
            try
            {
                var existingAdjunct = !request.CreateOnly && existing.TryGetValue(adjunct.Id, out var maybeAdjunct)
                    ? maybeAdjunct
                    : null;

                // flag remains true if it was true (tripped)
                anyUpdates = anyUpdates || existingAdjunct != null; // true if at least one is updating existing - this or previous

                var processed = await adjunctUpsertService.HandleAdjunct(adjunct, existingAdjunct, cancellationToken);
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
        

        foreach (var adjunct in adjuncts)
        {
            if (currentAdjuncts.TryGetValue(adjunct.Processed.Id, out var updated))
            {
                // this is a bit clunky, but we don't want to query individually for each - there could be a lot
                adjunct.Processed = updated;
            }
        }

        var failed = await TryIngestNotify(adjuncts, cancellationToken);

        if (failed.Count != 0)
        {
            return ModifyEntityResult<Adjunct[]>.Failure(
                $"Adjuncts with ids '{string.Join(", ", failed)}' for asset {assetId} failed submission for ingestion and will need to be resubmitted",
                WriteResult.Error);
        }
        
        return ModifyEntityResult<Adjunct[]>.Success( adjuncts.Select(a => a.Processed).ToArray(),
            anyUpdates ? WriteResult.Updated : WriteResult.Created);
    }

    private async Task<List<string>> TryIngestNotify(ICollection<AdjunctDocument> adjuncts, CancellationToken cancellationToken)
    {
        List<string> failed = [];
        List<NotificationRecord<Adjunct>> notifications = [];
        
        foreach (var adjunct in adjuncts)
        {
            if (adjunct.ToBeIngested)
            {
                var success = await notificationSender.SendIngestAdjunctRequest(adjunct.Processed, cancellationToken);
                if (!success)
                {
                    failed.Add(adjunct.Processed.Id);
                    continue;
                }
            }

            var adjunctModificationRecord = adjunct.IsUpdate
                ? NotificationRecord<Adjunct>.Update(adjunct.Original!, adjunct.Processed, adjunct.ToBeIngested)
                : NotificationRecord<Adjunct>.Create(adjunct.Processed);
            
            notifications.Add(adjunctModificationRecord);
        }

        await deliverableNotificationSender.SendDeliverableModifiedMessage(notifications, cancellationToken);

        return failed;
    }

}
