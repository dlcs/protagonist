using System.Collections.Generic;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Model.CustomerImage;
using DLCS.Repository;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Customer.Requests;

public interface IBulkAssetPatcher
{
    /// <summary>
    /// Updates assets with new values from bulk asset patch requests
    /// </summary>
    public Task<List<Asset>> UpdateAssets(UpdateAllImages request, CancellationToken cancellationToken);
}

public class BulkAssetPatcher(DlcsContext dlcsContext) : IBulkAssetPatcher
{
    public async Task<List<Asset>> UpdateAssets(UpdateAllImages request, CancellationToken cancellationToken = default)
    {
        switch (request.Field)
        {
            case "manifests":
                await UpdateManifests(request, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported field '{request.Field}'");
        };

        var updatedAssets = await dlcsContext.Images.AsNoTracking()
            .Where(i => i.Customer == request.CustomerId && request.AssetIds.Contains(i.Id))
            .ToListAsync(cancellationToken);
        
        return updatedAssets;
    }

    private async Task UpdateManifests(UpdateAllImages request, CancellationToken cancellationToken)
    {
        var requestValue = request.Value;
        
        if (requestValue.IsEmpty()) requestValue = null;

        switch (request.Operation)
        {
            case OperationType.Add:
                await dlcsContext.Images
                    .Where(a => request.AssetIds.Any(aid => aid == a.Id))
                    .ExecuteUpdateAsync(setters =>
                        setters.SetProperty(a => a.Manifests, a => a.Manifests.Concat(requestValue)), cancellationToken);
                break;
            case OperationType.Remove:
                var convertedAssetIds = $"{string.Join("','", request.AssetIds)}";
                foreach (var valueToRemove in requestValue ?? [])
                {
                    await dlcsContext.Database.ExecuteSqlAsync(
                        $"update \"Images\" set \"Manifests\" = array_remove(\"Manifests\", {valueToRemove}) where \"Id\" in ({convertedAssetIds})",
                        cancellationToken);
                }
                break;
            case OperationType.Replace:
                await dlcsContext.Images
                    .Where(a => request.AssetIds.Any(aid => aid == a.Id))
                    .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.Manifests, requestValue),
                        cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported operation '{request.Operation}'");
        }
    }
}
