using System.Collections.Generic;
using System.Data;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Assets;
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

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            foreach (var adjunct in request.Adjuncts)
            {
                var existingAdjunct = !request.CreateOnly && existing.TryGetValue(adjunct.Id, out var maybeAdjunct)
                    ? maybeAdjunct
                    : null;

                // flag remains true if it was true (tripped)
                anyUpdates = anyUpdates || existingAdjunct != null; // true if at least one is updating existing - this or previous

                var processed = await adjunctUpsertService.HandleAdjunct(adjunct, existingAdjunct, cancellationToken);
                adjuncts.Add(processed);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
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
        catch (Exception exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            logger.LogError(exception, "Error processing adjuncts for {AssetId}", assetId);
            return ModifyEntityResult<Adjunct[]>.Failure($"Unknown error processing adjuncts for {assetId}");
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

        if (!await adjunctUpsertService.SendNotifications(adjuncts, cancellationToken))
        {
            return ModifyEntityResult<Adjunct[]>.Failure(
                $"One or more adjuncts for asset {assetId} failed submission for ingestion and will need to be resubmitted",
                WriteResult.Error);
        }
        
        return ModifyEntityResult<Adjunct[]>.Success( adjuncts.Select(a => a.Processed).ToArray(),
            anyUpdates ? WriteResult.Updated : WriteResult.Created);
    }

}
