using System.Collections.Generic;
using System.Data;
using API.Features.Adjuncts;
using API.Infrastructure;
using API.Infrastructure.Requests;
using DLCS.AWS.SNS.Messaging;
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
    IBatchCompletedNotificationSender batchCompletedNotificationSender,
    ILogger<CreateAdjunctBatchHandler> logger)
    : IRequestHandler<CreateAdjunctBatch, ModifyEntityResult<AdjunctBatch>>
{
    public async Task<ModifyEntityResult<AdjunctBatch>> Handle(CreateAdjunctBatch request,
        CancellationToken cancellationToken)
    {
        var adjunctByAsset = request.Adjuncts.ToLookup(a => a.AssetId, a => a.Id);
        
        var validationError = await ValidateAssets(adjunctByAsset, cancellationToken);
        if (validationError != null) return validationError;

        // Load any existing adjuncts into a dictionary for ease of lookup
        var existing = (await dbContext.Adjuncts
                .FindAdjuncts(adjunctByAsset)
                .ToListAsync(cancellationToken))
            .ToDictionary(a => (a.AssetId, a.Id));
        
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var adjunctDocs = new List<AdjunctDocument>(request.Adjuncts.Length);
        AdjunctBatch? batch;

        try
        {
            // Upsert each adjunct in the batch
            foreach (var adjunct in request.Adjuncts)
            {
                existing.TryGetValue((adjunct.AssetId, adjunct.Id), out var dbAdjunct);
                adjunctDocs.Add(await adjunctUpsertService.HandleAdjunct(adjunct, dbAdjunct, cancellationToken));
            }

            // Once all adjuncts are upserted, create the batch + assign the BatchId to each adjunct
            logger.LogDebug("Creating batch for {CustomerId} with {AdjunctCount} adjuncts", request.CustomerId,
                adjunctDocs.Count);
            batch = await adjunctBatchRepository.CreateBatch(request.CustomerId,
                adjunctDocs.Select(d => d.Processed).ToList(), cancellationToken);

            // And finally create the batch-adjunct historical record
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

        // Post transaction commit: notifications cannot be rolled back; failures are logged, not surfaced as errors
        if (!await adjunctUpsertService.SendNotifications(adjunctDocs, cancellationToken))
        {
            logger.LogWarning(
                "Not all engine notifications sent for adjunct batch {BatchId} (customer {CustomerId}); some adjuncts may not be ingested",
                batch.Id, request.CustomerId);
        }

        if (batch.Finished.HasValue)
        {
            await batchCompletedNotificationSender.SendBatchCompletedMessage(batch, cancellationToken);
        }

        return ModifyEntityResult<AdjunctBatch>.Success(batch, WriteResult.Created);
    }

    private async Task<ModifyEntityResult<AdjunctBatch>?> ValidateAssets(ILookup<AssetId, string> adjunctByAsset,
        CancellationToken cancellationToken)
    {
        var assetIds = adjunctByAsset.Select(grp => grp.Key).ToList();
        var existingAssetIds = await dbContext.Images
            .Where(a => assetIds.Contains(a.Id))
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var missingAssets = assetIds.Except(existingAssetIds).ToList();
        if (missingAssets.Count > 0)
        {
            return ModifyEntityResult<AdjunctBatch>.Failure(
                $"Assets not found: {string.Join(", ", missingAssets)}",
                WriteResult.NotFound);
        }

        return null;
    }
}
