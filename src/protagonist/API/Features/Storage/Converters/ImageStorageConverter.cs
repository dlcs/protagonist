namespace API.Features.Storage.Converters;

/// <summary>
/// Conversion between API and EF forms of ImageStorage resource
/// </summary>
public static class ImageStorageConverter
{
    /// <summary>
    /// Convert ImageStorage entity to API resource
    /// </summary>
    public static DLCS.HydraModel.ImageStorage ToHydra(this DLCS.Model.Assets.ImageStorage imageStorage, string baseUrl)
    {
        var hydraImageStorage = new DLCS.HydraModel.ImageStorage(baseUrl, imageStorage.Customer,
            imageStorage.Space, imageStorage.Id.Asset)
        {
            ThumbnailSize = imageStorage.ThumbnailSize,
            Size = imageStorage.Size,
            LastChecked = imageStorage.LastChecked,
            CheckingInProgress = imageStorage.CheckingInProgress,
        };
        
        return hydraImageStorage;
    }
}