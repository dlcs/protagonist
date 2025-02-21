using DLCS.Model.Assets;
using Engine.Ingest.Models;
using Engine.Ingest.Persistence;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest;

public interface IAssetIngestorSizeCheck
{
    /// <summary>
    /// Check if customer is excluded from storage policy check
    /// </summary>
    bool CustomerHasNoStorageCheck(int customerId);
    
    /// <summary>
    /// Check if <see cref="AssetFromOrigin"/> exceeds storage allowance, if so set appropriate error on asset
    /// </summary>
    bool DoesAssetFromOriginExceedAllowance(AssetFromOrigin assetFromOrigin, Asset asset);
}

public abstract class AssetIngestorSizeCheckBase : IAssetIngestorSizeCheck
{
    public abstract bool CustomerHasNoStorageCheck(int customerId);

    public bool DoesAssetFromOriginExceedAllowance(AssetFromOrigin assetFromOrigin, Asset asset)
    {
        if (assetFromOrigin.FileExceedsAllowance)
        {
            asset.Error = IngestErrors.StoragePolicyExceeded;
            return true;
        }

        return false;
    }
}

/// <summary>
/// Implementation of <see cref="IAssetIngestorSizeCheck"/> using AppSettings driven config
/// </summary>
public class AppSettingsAssetIngestorSizeCheck : AssetIngestorSizeCheckBase
{
    private readonly EngineSettings engineSettings;
    public AppSettingsAssetIngestorSizeCheck(IOptionsMonitor<EngineSettings> engineOptions)
    {
        engineSettings = engineOptions.CurrentValue;
    }
    
    public override bool CustomerHasNoStorageCheck(int customerId)
    {
        var customerSpecific = engineSettings.GetCustomerSettings(customerId);
        return customerSpecific.NoStoragePolicyCheck;
    }
}