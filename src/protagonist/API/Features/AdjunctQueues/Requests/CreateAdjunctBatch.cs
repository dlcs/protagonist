using System.Collections.Generic;
using System.Data;
using API.Features.Adjuncts;
using API.Infrastructure;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository;
using DLCS.Repository.Adjuncts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Features.AdjunctQueues.Requests;

/// <summary>
/// Create a batch of <see cref="Adjunct"/> entities from the provided list
/// </summary>
public class CreateAdjunctBatch(int customerId, Adjunct[] adjuncts) : IRequest<ModifyEntityResult<AdjunctBatch>>
{
    public int CustomerId { get; } = customerId;
    public Adjunct[] Adjuncts { get; } = adjuncts;
}

public class CreateAdjunctBatchHandler(
    DlcsContext dbContext,
    IAdjunctBatchRepository adjunctBatchRepository,
    AdjunctUpsertService adjunctUpsertService,
    ILogger<CreateAdjunctBatchHandler> logger)
    : IRequestHandler<CreateAdjunctBatch, ModifyEntityResult<AdjunctBatch>>
{
    public async Task<ModifyEntityResult<AdjunctBatch>> Handle(CreateAdjunctBatch request,
        CancellationToken cancellationToken)
    {
        var adjunctByAsset = request.Adjuncts.ToLookup(a => a.AssetId, a => a.Id);
        var assetIds = adjunctByAsset.Select(grp => grp.Key).ToList();
        
        var validationError = await ValidateAssets(assetIds, cancellationToken);
        if (validationError != null) return validationError;

        var existing = (await dbContext.Adjuncts
                .FindAdjuncts(adjunctByAsset)
                .ToListAsync(cancellationToken))
            .ToDictionary(a => (a.AssetId, a.Id));
        
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var adjunctDocs = new List<AdjunctDocument>(request.Adjuncts.Length);
        AdjunctBatch? batch = null;

        try
        {
            foreach (var adjunct in request.Adjuncts)
            {
                existing.TryGetValue((adjunct.AssetId, adjunct.Id), out var dbAdjunct);
                adjunctDocs.Add(await adjunctUpsertService.HandleAdjunct(adjunct, dbAdjunct, cancellationToken));
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            batch = await adjunctBatchRepository.CreateBatch(request.CustomerId,
                adjunctDocs.Select(d => d.Processed).ToList(), cancellationToken);

            foreach (var doc in adjunctDocs)
            {
                batch.AddAdjunctBatchAdjunct(
                    doc.Processed.Id,
                    doc.Processed.AssetId,
                    doc.ToBeIngested ? BatchStatus.Waiting : BatchStatus.Completed);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception processing adjunct batch for customer {CustomerId}, rolling back",
                request.CustomerId);
            await transaction.RollbackAsync(CancellationToken.None);
            return ModifyEntityResult<AdjunctBatch>.Failure(ex.Message);
        }

        // Post-commit: notifications cannot be rolled back; failures are logged, not surfaced as errors
        if (!await adjunctUpsertService.SendNotifications(adjunctDocs, cancellationToken))
        {
            logger.LogWarning(
                "Not all engine notifications sent for adjunct batch (customer {CustomerId}); some adjuncts may not be ingested",
                request.CustomerId);
        }

        return ModifyEntityResult<AdjunctBatch>.Success(batch!, WriteResult.Created);
    }
    
    private async Task<ModifyEntityResult<AdjunctBatch>?> ValidateAssets(List<AssetId> assetIds, CancellationToken cancellationToken)
    {
        var existingAssetIds = await dbContext.Images
            .Where(a => assetIds.Contains(a.Id))
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var missingAssets = assetIds.Except(existingAssetIds).ToList();
        if (missingAssets.Count != 0)
        {
            return ModifyEntityResult<AdjunctBatch>.Failure(
                $"Assets not found: {string.Join(", ", missingAssets)}",
                WriteResult.NotFound);
        }

        return null;
    }

}
