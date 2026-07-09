using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using API.Infrastructure;
using API.Infrastructure.Messaging.General;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Repository;
using DLCS.Model.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Features.Adjuncts;

/// <summary>
/// Handles the core create-vs-update logic for a single adjunct, shared between
/// the single-adjunct and batch-adjunct handlers.
/// </summary>
public class AdjunctUpsertService(
    DlcsContext dbContext,
    IStorageRepository storageRepository,
    IIngestNotificationSender notificationSender,
    IDeliverableNotificationSender deliverableNotificationSender,
    ILogger<AdjunctUpsertService> logger)
{
    /// <summary>
    /// Prepares an adjunct for persistence: either adds it as new to the EF context or
    /// updates the tracked <paramref name="dbAdjunct"/> with values from <paramref name="adjunct"/>.
    /// Does not call SaveChanges — the caller is responsible for that.
    /// </summary>
    /// <param name="adjunct">Incoming adjunct with requested values.</param>
    /// <param name="dbAdjunct">Existing tracked entity, or null if this is a create.</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>An <see cref="AdjunctDocument"/> capturing the processed entity and whether it is new/updated.</returns>
    public async Task<AdjunctDocument> HandleAdjunct(Adjunct adjunct, Adjunct? dbAdjunct,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Processing adjunct {AdjunctIdentifier}", adjunct.Identifier());
        var isHosted = adjunct.IsHosted();
        Adjunct? existingAdjunct = null;

        if (dbAdjunct != null)
        {
            existingAdjunct = dbAdjunct.Clone();

            if (!isHosted)
            {
                if (dbAdjunct.IsHosted())
                {
                    // Was hosted, now external — decrement count and reduce stored size. Optimised adjuncts were
                    // never counted towards size (their bytes stayed in the origin), so only reduce size for others.
                    var storedSize = dbAdjunct.CountsTowardStoredSize() ? dbAdjunct.Size ?? 0 : 0;
                    await storageRepository.DecrementAdjunctStorage(adjunct.AssetId, storedSize, cancellationToken);
                }

                // External adjunct — size is irrelevant for storage limits, copy submitted value
                dbAdjunct.Size = adjunct.Size;
            }
            else if (!dbAdjunct.IsHosted())
            {
                // Was external, now hosted — reset size so Engine calculates from scratch
                dbAdjunct.Size = null;
                await IncrementAdjunctCount(adjunct.AssetId.Customer, adjunct.AssetId.Space, cancellationToken);
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

            if (isHosted)
            {
                // Engine will set the real size; disregard any submitted value
                dbAdjunct.Size = null;
                await IncrementAdjunctCount(adjunct.AssetId.Customer, adjunct.AssetId.Space, cancellationToken);
            }

            await dbContext.Adjuncts.AddAsync(dbAdjunct, cancellationToken);
        }

        if (!isHosted)
        {
            // External adjunct — no Engine involvement, so finalise now
            dbAdjunct.Finished = DateTime.UtcNow;
        }

        return new AdjunctDocument(dbAdjunct, existingAdjunct);
    }

    private Task IncrementAdjunctCount(int customerId, int space, CancellationToken cancellationToken) =>
        dbContext.CustomerStorages
            .Where(cs => cs.Customer == customerId && (cs.Space == null || cs.Space == space))
            .UpdateFromQueryAsync(cs => new CustomerStorage
            {
                NumberOfStoredAdjuncts = cs.NumberOfStoredAdjuncts + 1
            }, cancellationToken);

    /// <summary>
    /// Sends engine ingest and deliverable-modified notifications for a processed set of adjuncts.
    /// </summary>
    /// <returns>True if all notifications were sent successfully.</returns>   
    public async Task<bool> SendNotifications(ICollection<AdjunctDocument> adjuncts, CancellationToken cancellationToken)
    {
        var toIngest = adjuncts
            .Where(a => a.IsHosted)
            .Select(a => a.Processed)
            .ToList();

        bool allSent = true;
        if (toIngest.Count > 0)
        {
            var sent = await notificationSender.SendIngestAdjunctRequest(toIngest, cancellationToken);
            allSent = sent == toIngest.Count;
        }

        var notifications = adjuncts
            .Select(a => a.IsUpdate
                ? NotificationRecord<Adjunct>.Update(a.Original, a.Processed, a.IsHosted)
                : NotificationRecord<Adjunct>.Create(a.Processed))
            .ToList();

        await deliverableNotificationSender.SendDeliverableModifiedMessage(notifications, cancellationToken);
        return allSent;
    }
}

/// <summary>
/// Wraps a processed adjunct entity together with its pre-update snapshot and ingest intent.
/// </summary>
public class AdjunctDocument(Adjunct adjunct, Adjunct? existingAdjunct)
{
    public bool IsHosted { get; } = adjunct.IsHosted();
    
    [MemberNotNullWhen(true, nameof(Original))]
    public bool IsUpdate => Original != null;
    
    public Adjunct? Original { get; } = existingAdjunct;
    
    public Adjunct Processed { get; set; } = adjunct;
}
