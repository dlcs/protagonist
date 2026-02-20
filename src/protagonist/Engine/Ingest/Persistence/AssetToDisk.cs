using DLCS.Core;
using DLCS.Core.Guard;
using DLCS.Core.Streams;
using DLCS.Core.Strings;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Storage;
using DLCS.Repository.Strategy;
using DLCS.Repository.Strategy.Utils;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Persistence;

public interface IAssetToDisk
{
    /// <summary>
    /// Copy asset from Origin to local disk.
    /// </summary>
    /// <param name="context">Ingestion context containing the <see cref="Asset"/> to be copied.</param>
    /// <param name="destinationTemplate">String representing destinations folder to copy to.</param>
    /// <param name="verifySize">if True, size is validated that it does not exceed allowed size.</param>
    /// <param name="customerOriginStrategy"><see cref="CustomerOriginStrategy"/> to use to fetch item.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/>Current cancellation token</param>
    /// <returns><see cref="AssetFromOrigin"/> containing new location, size etc</returns>
    Task<AssetFromOrigin> CopyAssetToLocalDisk(IngestionContext context, string destinationTemplate, bool verifySize,
        CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copy adjunct from Origin to local disk.
    /// </summary>
    /// <param name="context">Ingestion context containing the <see cref="Asset"/> to be copied.</param>
    /// <param name="destinationTemplate">String representing destinations folder to copy to.</param>
    /// <param name="verifySize">if True, size is validated that it does not exceed allowed size.</param>
    /// <param name="customerOriginStrategy"><see cref="CustomerOriginStrategy"/> to use to fetch item.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/>Current cancellation token</param>
    /// <returns><see cref="AdjunctFromOrigin"/> containing new location, size etc</returns>
    Task<AdjunctFromOrigin> CopyAdjunctToLocalDisk(AdjunctIngestionContext context,
        string destinationTemplate, bool verifySize,
        CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Class for copying asset from origin to local disk.
/// </summary>
public class AssetToDisk(
    OriginFetcher originFetcher,
    IStorageRepository storageRepository,
    IFileSaver fileSaver,
    IOptionsMonitor<EngineSettings> engineOptions,
    ILogger<AssetToDisk> logger)
    : AssetMoverBase(storageRepository), IAssetToDisk
{
    private readonly EngineSettings engineSettings = engineOptions.CurrentValue;
    
     
    /// <summary>
    /// Copy asset from Origin to local disk.
    /// </summary>
    /// <param name="context">Ingestion context containing the <see cref="Asset"/> to be copied.</param>
    /// <param name="destinationTemplate">String representing destinations folder to copy to.</param>
    /// <param name="verifySize">if True, size is validated that it does not exceed allowed size.</param>
    /// <param name="customerOriginStrategy"><see cref="CustomerOriginStrategy"/> to use to fetch item.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/>Current cancellation token</param>
    /// <returns><see cref="AssetFromOrigin"/> containing new location, size etc</returns>
    public async Task<AssetFromOrigin> CopyAssetToLocalDisk(IngestionContext context, string destinationTemplate,
        bool verifySize,
        CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default)
    {
        destinationTemplate.ThrowIfNullOrWhiteSpace(nameof(destinationTemplate));

        await using var originResponse =
            await originFetcher.LoadFromOrigin(context.Asset,
                customerOriginStrategy, cancellationToken);

        if (originResponse.Stream.IsNull())
        {
            logger.LogWarning("Unable to fetch asset {AssetId} from {Origin}, using {OriginStrategy}", context.Asset.Id,
                context.Asset.Origin, customerOriginStrategy.Strategy);
            throw new ApplicationException(
                $"Unable to get asset '{context.Asset.Id}' from origin '{context.Asset.Origin}' using {customerOriginStrategy.Strategy}");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var assetFromOrigin =
            await CopyAssetToDisk(context.Asset, destinationTemplate, originResponse, cancellationToken);
        assetFromOrigin.CustomerOriginStrategy = customerOriginStrategy;

        if (verifySize)
        {
            await VerifyFileSize(context, assetFromOrigin);
        }

        return assetFromOrigin;
    }

    /// <summary>
    /// Copy adjunct from Origin to local disk.
    /// </summary>
    /// <param name="context">Ingestion context containing the <see cref="Adjunct"/> to be copied.</param>
    /// <param name="destinationTemplate">String representing destinations folder to copy to.</param>
    /// <param name="verifySize">if True, size is validated that it does not exceed allowed size.</param>
    /// <param name="customerOriginStrategy"><see cref="CustomerOriginStrategy"/> to use to fetch item.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/>Current cancellation token</param>
    /// <returns><see cref="AdjunctFromOrigin"/> containing new location, size etc</returns>
    public async Task<AdjunctFromOrigin> CopyAdjunctToLocalDisk(AdjunctIngestionContext context,
        string destinationTemplate, bool verifySize,
        CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default)
    {
        destinationTemplate.ThrowIfNullOrWhiteSpace(nameof(destinationTemplate));
        
        await using var originResponse =
            await originFetcher.LoadFromOrigin(context.Adjunct,
                customerOriginStrategy, cancellationToken);

        if (originResponse.Stream.IsNull())
        {
            logger.LogWarning("Unable to fetch asset {AssetId} from {Origin}, using {OriginStrategy}", context.Asset.Id,
                context.Asset.Origin, customerOriginStrategy.Strategy);
            throw new ApplicationException(
                $"Unable to get asset '{context.Asset.Id}' from origin '{context.Asset.Origin}' using {customerOriginStrategy.Strategy}");
        }

        cancellationToken.ThrowIfCancellationRequested();
        
        var assetFromOrigin =
            await CopyAdjunctToDisk(context.Adjunct.Id, context.Asset, destinationTemplate, originResponse,
                cancellationToken);
        
        assetFromOrigin.CustomerOriginStrategy = customerOriginStrategy;

        if (verifySize)
        {
            await VerifyFileSize(context, assetFromOrigin);
        }

        return assetFromOrigin;
    }

    private async Task<AdjunctFromOrigin> CopyAdjunctToDisk(string adjunctId, Asset asset, string destinationTemplate,
        OriginResponse originResponse, CancellationToken cancellationToken)
    {
        TrySetContentTypeForBinary(originResponse, asset);
        var extension = GetFileExtension(originResponse);

        var path = Path.Join(destinationTemplate, asset.Id.GetDiskSafeAssetId(engineSettings.ImageIngest), "adjuncts",
            adjunctId.GetDiskSafeFileId(engineSettings.ImageIngest));

        var targetPath = $"{path}.{extension}";

        var received = await fileSaver.SaveResponseToDisk(asset, originResponse, targetPath,
            cancellationToken);

        return new AdjunctFromOrigin(adjunctId, asset.Id, received, targetPath, originResponse.ContentType);
    }
    
    private async Task<AssetFromOrigin> CopyAssetToDisk(Asset asset, string destinationTemplate,
        OriginResponse originResponse, CancellationToken cancellationToken)
    {
        TrySetContentTypeForBinary(originResponse, asset);
        var extension = GetFileExtension(originResponse);

        var path = Path.Join(destinationTemplate, asset.Id.GetDiskSafeAssetId(engineSettings.ImageIngest));

        var targetPath = $"{path}.{extension}";

        var received = await fileSaver.SaveResponseToDisk(asset, originResponse, targetPath,
            cancellationToken);

        return new AssetFromOrigin(asset.Id, received, targetPath, originResponse.ContentType);
    }

    // TODO - this may need refined depending on whether it's 'I' or 'T' ingest
    private void TrySetContentTypeForBinary(OriginResponse originResponse, Asset asset)
    {
        string? GuessContentType(string source)
        {
            var extension = source.EverythingAfterLast('.');
            var guess = MIMEHelper.GetContentTypeForExtension(extension);
            return guess;
        }

        // If the content type is binary, attempt to determine via file extension on name
        var contentType = originResponse.ContentType;
        if (string.IsNullOrWhiteSpace(contentType) || IsBinaryContent(contentType))
        {
            var uniqueName = asset.Id.Asset;

            var guess = GuessContentType(asset.Origin);
            if (string.IsNullOrEmpty(guess))
            {
                guess = GuessContentType(uniqueName);
                logger.LogDebug("Guessed content type as {ContentType} for '{AssetName}' from uniqueId", guess,
                    uniqueName);
            }
            else
            {
                logger.LogDebug("Guessed content type as {ContentType} for '{AssetName}' from origin", guess,
                    uniqueName);
            }

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

    private async Task VerifyFileSize(IngestionContext context, AssetFromOrigin assetFromOrigin)
    {
        var customerHasEnoughSize = await VerifyFileSize(context.Asset.Customer, assetFromOrigin.AssetSize,
            context.PreIngestionAssetSize);

        if (!customerHasEnoughSize)
        {
            assetFromOrigin.FileTooLarge();
        }
    }
}
