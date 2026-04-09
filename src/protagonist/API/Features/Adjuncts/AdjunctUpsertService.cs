using DLCS.Model.Assets;
using DLCS.Repository;

namespace API.Features.Adjuncts;

/// <summary>
/// Handles the core create-vs-update logic for a single adjunct, shared between
/// the single-adjunct and batch-adjunct handlers.
/// </summary>
public class AdjunctUpsertService(DlcsContext dbContext)
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
        var toBeIngested = adjunct.IsToBeIngested();
        Adjunct? existingAdjunct = null;

        if (dbAdjunct != null)
        {
            existingAdjunct = dbAdjunct.Clone();

            if (!toBeIngested)
            {
                // External adjunct — size is irrelevant for storage limits, copy submitted value
                dbAdjunct.Size = adjunct.Size;
            }
            else if (!dbAdjunct.IsToBeIngested())
            {
                // Was external, now hosted — reset size so Engine calculates from scratch
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
                // Engine will set the real size; disregard any submitted value
                dbAdjunct.Size = null;
            }

            await dbContext.Adjuncts.AddAsync(dbAdjunct, cancellationToken);
        }

        if (!toBeIngested)
        {
            // External adjunct — no Engine involvement, so finalise now
            dbAdjunct.Finished = DateTime.UtcNow;
        }

        return new AdjunctDocument(dbAdjunct, existingAdjunct);
    }
}

/// <summary>
/// Wraps a processed adjunct entity together with its pre-update snapshot and ingest intent.
/// </summary>
public class AdjunctDocument(Adjunct adjunct, Adjunct? existingAdjunct)
{
    public bool ToBeIngested { get; } = adjunct.IsToBeIngested();
    public bool IsUpdate => Original != null;
    public Adjunct? Original { get; } = existingAdjunct;
    public Adjunct Processed { get; set; } = adjunct;
}
