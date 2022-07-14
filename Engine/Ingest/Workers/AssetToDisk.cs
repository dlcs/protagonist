using DLCS.Core;
using DLCS.Core.Guard;
using DLCS.Core.Streams;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Storage;
using DLCS.Repository.Strategy;
using DLCS.Repository.Strategy.Utils;

namespace Engine.Ingest.Workers;

/// <summary>
/// Class for copying asset from origin to local disk.
/// </summary>
public class AssetToDisk : IAssetMover
{
    private readonly IStorageRepository storageRepository;
    private readonly FileSaver fileSaver;
    private readonly ILogger<AssetToDisk> logger;
    private readonly Dictionary<OriginStrategyType, IOriginStrategy> originStrategies;

    public AssetToDisk(
        IEnumerable<IOriginStrategy> originStrategies,
        IStorageRepository storageRepository,
        FileSaver fileSaver,
        ILogger<AssetToDisk> logger)
    {
        this.storageRepository = storageRepository;
        this.fileSaver = fileSaver;
        this.logger = logger;
        this.originStrategies = originStrategies.ToDictionary(k => k.Strategy, v => v);
    }
    
    /// <summary>
    /// Copy asset from Origin to local disk.
    /// </summary>
    /// <param name="asset"><see cref="Asset"/> to be copied.</param>
    /// <param name="destinationTemplate">String representing destinations folder to copy to.</param>
    /// <param name="verifySize">if True, size is validated that it does not exceed allowed size.</param>
    /// <param name="customerOriginStrategy"><see cref="CustomerOriginStrategy"/> to use to fetch item.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/>Current cancellation token</param>
    /// <returns><see cref="AssetFromOrigin"/> containing new location, size etc</returns>
    public async Task<AssetFromOrigin> CopyAsset(Asset asset, string destinationTemplate, bool verifySize, 
        CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default)
    {
        destinationTemplate.ThrowIfNullOrWhiteSpace(nameof(destinationTemplate));
        
        if (!originStrategies.TryGetValue(customerOriginStrategy.Strategy, out var strategy))
        {
            throw new InvalidOperationException(
                $"No OriginStrategy implementation found for '{customerOriginStrategy.Strategy}' strategy (id: {customerOriginStrategy.Id})");
        }

        await using var originResponse =
            await strategy.LoadAssetFromOrigin(asset.GetAssetId(), asset.GetIngestOrigin(), customerOriginStrategy,
                cancellationToken);

        if (originResponse == null || originResponse.Stream.IsNull())
        {
            logger.LogWarning("Unable to fetch asset {AssetId} from {Origin} using {Strategy}", asset.Id, asset.Origin,
                strategy.Strategy);
            throw new ApplicationException($"Unable to get asset '{asset.Id}' from origin '{asset.Origin}'");
        }
        
        cancellationToken.ThrowIfCancellationRequested();
        var assetFromOrigin = await CopyAssetToDisk(asset, destinationTemplate, originResponse, cancellationToken);
        assetFromOrigin.CustomerOriginStrategy = customerOriginStrategy;
        
        if (verifySize)
        {
            await VerifyFileSize(asset, assetFromOrigin);
        }
            
        return assetFromOrigin;
    }

    private async Task<AssetFromOrigin> CopyAssetToDisk(Asset asset, string destinationTemplate,
        OriginResponse originResponse, CancellationToken cancellationToken)
    {
        TrySetContentTypeForBinary(originResponse, asset);
        var extension = GetFileExtension(originResponse);
        
        var targetPath = $"{Path.Join(destinationTemplate, asset.GetUniqueName())}.{extension}";

        var received = await fileSaver.SaveResponseToDisk(asset.GetAssetId(), originResponse, targetPath,
            cancellationToken);
        
        return new AssetFromOrigin(asset.Id, received, targetPath, originResponse.ContentType);
    }
    
    // TODO - this may need refined depending on whether it's 'I' or 'T' ingest
    private void TrySetContentTypeForBinary(OriginResponse originResponse, Asset asset)
    {
        // If the content type is binary, attempt to determine via file extension on name
        var contentType = originResponse.ContentType;
        if (string.IsNullOrWhiteSpace(contentType) || IsBinaryContent(contentType))
        {
            var uniqueName = asset.GetUniqueName();
            var extension = uniqueName[uniqueName.LastIndexOf(".", StringComparison.Ordinal)..];

            var guess = MIMEHelper.GetContentTypeForExtension(extension);
            logger.LogDebug("Guessed content type as {ContentType} for '{AssetName}'", guess, uniqueName);
            originResponse.WithContentType(guess);
        }
    }

    private static bool IsBinaryContent(string contentType) =>
        contentType is MIMEHelper.ApplicationOctet or MIMEHelper.BinaryOctet;
    
    private string GetFileExtension(OriginResponse originResponse)
    {
        var extension = MIMEHelper.GetExtensionForContentType(originResponse.ContentType);

        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = "file";
            logger.LogInformation("Unable to get a file extension for {ContentType}", originResponse.ContentType);
        }

        return extension;
    }
    
    private async Task VerifyFileSize(Asset asset, AssetFromOrigin assetFromOrigin)
    {
        var customerHasEnoughSize = await storageRepository.VerifyStoragePolicyBySize(asset.Customer,
            assetFromOrigin.AssetSize);

        if (!customerHasEnoughSize)
        {
            assetFromOrigin.FileTooLarge();
        }
    }
}