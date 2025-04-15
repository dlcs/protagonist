using System.Collections.Generic;
using API.Infrastructure.Requests;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Customer;

public interface IBulkAssetPatcher
{
    /// <summary>
    /// Updates assets with new values from bulk asset patch requests
    /// </summary>
    public Task<List<Asset>> UpdateAssets(List<AssetId> assetIds, List<string>? value, OperationType operation,
        string field, int customerId, CancellationToken cancellationToken = default);
}

public class BulkAssetPatcher(DlcsContext dlcsContext) : IBulkAssetPatcher
{
    public async Task<List<Asset>> UpdateAssets(List<AssetId> assetIds, List<string>? value, OperationType operation, 
        string field, int customerId, CancellationToken cancellationToken = default)
    {
        switch (field)
        {
            case SupportedFields.ManifestField:
                await UpdateManifests(assetIds, value, operation, customerId, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported field '{field}'");
        };

        var updatedAssets = await dlcsContext.Images.AsNoTracking()
            .Where(i => i.Customer == customerId && assetIds.Contains(i.Id))
            .ToListAsync(cancellationToken);
        
        return updatedAssets;
    }

    private async Task UpdateManifests(List<AssetId> assetIds, List<string>? value, OperationType operation, int customerId,
        CancellationToken cancellationToken)
    {
        if (value.IsEmpty()) value = null;

        switch (operation)
        {
            case OperationType.Add:
                await dlcsContext.Images
                    .Where(a => assetIds.Any(aid => aid == a.Id) && a.Customer == customerId)
                    .ExecuteUpdateAsync(setters =>
                        setters.SetProperty(a => a.Manifests, a => a.Manifests.Concat(value)), cancellationToken);
                break;
            case OperationType.Remove:
                var convertedAssetIds = $"{string.Join("','", assetIds)}";
                foreach (var valueToRemove in value ?? [])
                {
                    await dlcsContext.Database.ExecuteSqlAsync(
                        $"update \"Images\" set \"Manifests\" = array_remove(\"Manifests\", {valueToRemove}) where \"Id\" in ({convertedAssetIds}) and \"Customer\" = {customerId}",
                        cancellationToken);
                }
                break;
            case OperationType.Replace:
                await dlcsContext.Images
                    .Where(a => assetIds.Any(aid => aid == a.Id) && a.Customer == customerId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.Manifests, value),
                        cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported operation '{operation}'");
        }
    }
    
    public class SupportedFields
    {
        public const string ManifestField = "manifests";
    }
}
