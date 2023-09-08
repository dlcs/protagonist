namespace API.Features.Storage.Converters;

public static class ImageStorageConverter
{
    public static DLCS.HydraModel.ImageStorage ToHydra(this DLCS.Model.Assets.ImageStorage imageStorage, string baseUrl)
    {
        var hydraImageStorage = new DLCS.HydraModel.ImageStorage(baseUrl, imageStorage.Customer,
            imageStorage.Space, imageStorage.Id.Asset)
        {
            ThumbnailSize = (int)imageStorage.ThumbnailSize,
            Size = (int)imageStorage.Size,
            LastChecked = imageStorage.LastChecked,
            CheckingInProgress = imageStorage.CheckingInProgress,
        };
        
        return hydraImageStorage;
    }
}