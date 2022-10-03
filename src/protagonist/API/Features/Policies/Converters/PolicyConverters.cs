namespace API.Features.Policies.Converters;

using HydraOptimisationPolicy = DLCS.HydraModel.ImageOptimisationPolicy;
using EntityOptimisationPolicy = DLCS.Model.Policies.ImageOptimisationPolicy;

using HydraStoragePolicy = DLCS.HydraModel.StoragePolicy;
using EntityStoragePolicy = DLCS.Model.Storage.StoragePolicy;

using HydraThumbPolicy = DLCS.HydraModel.ThumbnailPolicy;
using EntityThumbPolicy = DLCS.Model.Policies.ThumbnailPolicy;

using HydraOriginStrategy = DLCS.HydraModel.OriginStrategy;
using EntityOriginStrategy = DLCS.Model.Policies.OriginStrategy;

public static class PolicyConverters
{
    /// <summary>
    /// Convert ImageOptimisationPolicy entity to API resource
    /// </summary>
    public static HydraOptimisationPolicy ToHydra(this EntityOptimisationPolicy policy, string baseUrl)
    {
        var hydra = new HydraOptimisationPolicy(baseUrl, policy.Id, policy.Name, string.Join(",", policy.TechnicalDetails));
        return hydra;
    }
    
    /// <summary>
    /// Convert StoragePolicy entity to API resource
    /// </summary>
    public static HydraStoragePolicy ToHydra(this EntityStoragePolicy policy, string baseUrl)
    {
        var hydra = new HydraStoragePolicy(baseUrl, policy.Id)
        {
            MaximumNumberOfStoredImages = policy.MaximumNumberOfStoredImages,
            MaximumTotalSizeOfStoredImages = policy.MaximumTotalSizeOfStoredImages
        };
        return hydra;
    }
    
    /// <summary>
    /// Convert ThumbnailPolicy entity to API resource
    /// </summary>
    public static HydraThumbPolicy ToHydra(this EntityThumbPolicy policy, string baseUrl)
    {
        var hydra = new HydraThumbPolicy(baseUrl, policy.Id)
        {
            Name = policy.Name,
            Sizes = policy.SizeList.ToArray()
        };
        return hydra;
    }
    
    /// <summary>
    /// Convert OriginStrategy entity to API resource
    /// </summary>
    public static HydraOriginStrategy ToHydra(this EntityOriginStrategy policy, string baseUrl)
    {
        var hydra = new HydraOriginStrategy(baseUrl, policy.Id)
        {
            RequiresCredentials = policy.RequiresCredentials
        };
        return hydra;
    }
}