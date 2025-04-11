using System.Collections.Generic;
using API.Features.Customer.Validation;
using DLCS.Core.Collections;
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
        var assetIds = ImageIdListValidation.ValidateRequest(request.HydraUpdate.Members!.Select(m => m.Id).ToList(),
            request.CustomerId);

        var assets = await dlcsContext.Images
            .Where(i => i.Customer == request.CustomerId && assetIds.Contains(i.Id))
            .IncludeDeliveryChannelsWithPolicy()
            .ToListAsync(cancellationToken);

        var updatedAssets = request.HydraUpdate.Field switch
        {
            "manifests" => UpdateManifests(request.HydraUpdate, assets),
            _ => throw new InvalidOperationException($"Unsupported field '{request.HydraUpdate.Field}'"),
        };
        
        await dlcsContext.SaveChangesAsync(cancellationToken);
        return updatedAssets;
    }

    private static List<Asset> UpdateManifests(HydraUpdate<IdentifierOnly> hydraUpdate, List<Asset> assets)
    {
        var convertedValuesJArray = hydraUpdate.Value as JArray;
        var convertedValues = convertedValuesJArray?.ToObject<List<string>>();

        if (convertedValues == null) throw new InvalidOperationException($"Unsupported value '{hydraUpdate.Value}'");
        
        if (convertedValues.IsEmpty()) convertedValues = null;

        switch (hydraUpdate.Operation)
        {
            case OperationType.Add:
                assets.ForEach(a =>
                    a.Manifests = a.Manifests != null ? a.Manifests.Concat(convertedValues).ToList() : convertedValues);
                break;
            case OperationType.Remove:
                assets.ForEach(a => a.Manifests?.RemoveAll(m => convertedValues.Any(v => m == v)));
                break;
            case OperationType.Replace:
                assets.ForEach(a => a.Manifests = convertedValues);
                break;
            default:
                throw new InvalidOperationException($"Unsupported operation '{hydraUpdate.Operation}'");
                break;
        }
        
        return assets;
    }
}
