using System.Collections.Generic;
using API.Features.Customer.Validation;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.HydraModel;
using DLCS.Model;
using DLCS.Model.Assets;
using DLCS.Repository;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

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
        var assetIds = ImageIdListValidation.ValidateRequest(request.HydraBulkPatch.Members!.Select(m => m.Id).ToList(),
            request.CustomerId);

        switch (request.HydraBulkPatch.Field)
        {
            case "manifests":
                await UpdateManifests(request.HydraBulkPatch, assetIds, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported field '{request.HydraBulkPatch.Field}'");
        };
        
        //await dlcsContext.SaveChangesAsync(cancellationToken);
        
        var updatedAssets = await dlcsContext.Images.AsNoTracking()
            .Where(i => i.Customer == request.CustomerId && assetIds.Contains(i.Id))
            .ToListAsync(cancellationToken);
        
        return updatedAssets;
    }

    private async Task UpdateManifests(BulkPatch<IdentifierOnly> hydraBulkPatch, List<AssetId> assetIds, CancellationToken cancellationToken)
    {
        if (hydraBulkPatch.Value == null) throw new InvalidOperationException($"Unsupported value '{hydraBulkPatch.Value}'");
        
        if (hydraBulkPatch.Value.IsEmpty()) hydraBulkPatch.Value = null;

        switch (hydraBulkPatch.Operation)
        {
            case OperationType.Add:
                await dlcsContext.Images
                    .Where(a => assetIds.Any(aid => aid == a.Id))
                    .ExecuteUpdateAsync(setters =>
                        setters.SetProperty(a => a.Manifests, a => a.Manifests.Concat(hydraBulkPatch.Value)), cancellationToken);
                break;
            case OperationType.Remove:
                var convertedAssetIds = $"{string.Join("','", assetIds)}";
                foreach (var valueToRemove in hydraBulkPatch.Value ?? [])
                {
                    await dlcsContext.Database.ExecuteSqlAsync(
                        $"update \"Images\" set \"Manifests\" = array_remove(\"Manifests\", {valueToRemove}) where \"Id\" in ({convertedAssetIds})",
                        cancellationToken);
                }
                break;
            case OperationType.Replace:
                await dlcsContext.Images
                    .Where(a => assetIds.Any(aid => aid == a.Id))
                    .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.Manifests, hydraBulkPatch.Value), cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported operation '{hydraBulkPatch.Operation}'");
        }
    }
}
