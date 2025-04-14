using System.Collections.Generic;
using System.Text;
using AngleSharp.Common;
using API.Features.Customer.Validation;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model;
using DLCS.Model.Assets;
using DLCS.Repository;
using DLCS.Repository.Assets;
using Hydra.Collections;
using Hydra.Model;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace API.Features.Customer.Requests;

public interface IAssetUpdater
{
    /// <summary>
    /// Updates assets with new values from all image requests
    /// </summary>
    public Task<List<Asset>> UpdateAssets(UpdateAllImages request, CancellationToken cancellationToken);
}

public class AssetUpdater(DlcsContext dlcsContext) : IAssetUpdater
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

    private async Task UpdateManifests(HydraBulkPatch<IdentifierOnly> hydraUpdate, List<AssetId> assetIds, CancellationToken cancellationToken)
    {
        var convertedValuesJArray = hydraUpdate.Value as JArray;
        var convertedValues = convertedValuesJArray?.ToObject<List<string>>();

        if (convertedValues == null) throw new InvalidOperationException($"Unsupported value '{hydraUpdate.Value}'");
        
        if (convertedValues.IsEmpty()) convertedValues = null;

        switch (hydraUpdate.Operation)
        {
            case OperationType.Add:
                await dlcsContext.Images
                    .Where(a => assetIds.Any(aid => aid == a.Id))
                    .ExecuteUpdateAsync(setters =>
                        setters.SetProperty(a => a.Manifests, a => a.Manifests.Concat(convertedValues)), cancellationToken);
                break;
            case OperationType.Remove:
                var convertedAssetIds = $"{string.Join("','", assetIds)}";
                foreach (var valueToRemove in convertedValues ?? [])
                {
                    await dlcsContext.Database.ExecuteSqlAsync(
                        $"update \"Images\" set \"Manifests\" = array_remove(\"Manifests\", {valueToRemove}) where \"Id\" in ({convertedAssetIds})",
                        cancellationToken);
                }
                break;
            case OperationType.Replace:
                await dlcsContext.Images
                    .Where(a => assetIds.Any(aid => aid == a.Id))
                    .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.Manifests, convertedValues), cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported operation '{hydraUpdate.Operation}'");
        }
    }
}
