using System.Collections.Generic;
using System.Data;
using API.Features.Adjuncts;
using API.Infrastructure;
using API.Infrastructure.Messaging.General;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
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
    IIngestNotificationSender notificationSender,
    IDeliverableNotificationSender deliverableNotificationSender,
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

        try
        {
            foreach (var adjunct in request.Adjuncts)
            {
                existing.TryGetValue((adjunct.AssetId, adjunct.Id), out var dbAdjunct);
                var doc = await adjunctUpsertService.HandleAdjunct(adjunct, dbAdjunct, cancellationToken);
                adjunctDocs.Add(doc);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var allAdjuncts = adjunctDocs.Select(d => d.Processed).ToList();
            var batch = await adjunctBatchRepository.CreateBatch(request.CustomerId, allAdjuncts, cancellationToken);

            foreach (var doc in adjunctDocs)
            {
                batch.AddAdjunctBatchAdjunct(
                    doc.Processed.Id,
                    doc.Processed.AssetId,
                    doc.ToBeIngested ? BatchStatus.Waiting : BatchStatus.Completed);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await TryIngestNotify(adjunctDocs, cancellationToken);

            return ModifyEntityResult<AdjunctBatch>.Success(batch, WriteResult.Created);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception processing adjunct batch for customer {CustomerId}, rolling back",
                request.CustomerId);
            await transaction.RollbackAsync(CancellationToken.None);
            return ModifyEntityResult<AdjunctBatch>.Failure(ex.Message);
        }
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

    private async Task TryIngestNotify(ICollection<AdjunctDocument> adjuncts, CancellationToken cancellationToken)
    {
        var toIngest = adjuncts
            .Where(a => a.ToBeIngested)
            .Select(a => a.Processed)
            .ToList();

        if (toIngest.Count > 0)
        {
            var sent = await notificationSender.SendIngestAdjunctRequest(toIngest, cancellationToken);
            if (sent != toIngest.Count)
            {
                logger.LogWarning(
                    "Only {Sent}/{Total} engine notifications sent for adjunct batch; some adjuncts may not be ingested",
                    sent, toIngest.Count);
            }
        }

        var notifications = adjuncts
            .Select(a => a.IsUpdate
                ? NotificationRecord<Adjunct>.Update(a.Original!, a.Processed, a.ToBeIngested)
                : NotificationRecord<Adjunct>.Create(a.Processed))
            .ToList();

        await deliverableNotificationSender.SendDeliverableModifiedMessage(notifications, cancellationToken);
    }
}
