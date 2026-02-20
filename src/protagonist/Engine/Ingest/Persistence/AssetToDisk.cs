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
    /// Copy item from Origin to local disk.
    /// </summary>
    /// <param name="context">Ingestion context containing the <see cref="IOriginItem"/> to be copied.</param>
    /// <param name="destinationTemplate">String representing destinations folder to copy to.</param>
    /// <param name="verifySize">if True, size is validated that it does not exceed allowed size.</param>
    /// <param name="customerOriginStrategy"><see cref="CustomerOriginStrategy"/> to use to fetch item.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/>Current cancellation token</param>
    /// <returns><see cref="AssetFromOrigin"/> containing new location, size etc</returns>
    /// <remarks>
    /// This method can also take <see cref="AdjunctIngestionContext"/> which overrides certain methods,
    /// resulting in copy of the Adjunct to appropriate adjuncts location, not the parent asset itself
    /// </remarks>
    Task<AssetFromOrigin> CopyItemToLocalDisk(IngestionContext context, string destinationTemplate, bool verifySize,
        CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Class for copying items from origin to local disk.
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
    /// <param name="context">Ingestion context containing the <see cref="IOriginItem"/> to be copied.</param>
    /// <param name="destinationTemplate">String representing destinations folder to copy to.</param>
    /// <param name="verifySize">if True, size is validated that it does not exceed allowed size.</param>
    /// <param name="customerOriginStrategy"><see cref="CustomerOriginStrategy"/> to use to fetch item.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/>Current cancellation token</param>
    /// <returns><see cref="AssetFromOrigin"/> containing new location, size etc</returns>
    public async Task<AssetFromOrigin> CopyItemToLocalDisk(IngestionContext context, string destinationTemplate,
        bool verifySize,
        CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default)
    {
        destinationTemplate.ThrowIfNullOrWhiteSpace(nameof(destinationTemplate));
        var item = context.GetOriginItem();

        await using var originResponse =
            await originFetcher.LoadFromOrigin(item, customerOriginStrategy, cancellationToken);

        if (originResponse.Stream.IsNull())
        {
            logger.LogWarning("Unable to fetch asset {Item} from {Origin}, using {OriginStrategy}", item.Identifier(),
                item.Origin, customerOriginStrategy.Strategy);
            throw new ApplicationException(
                $"Unable to get item {item.Identifier()} from origin '{item.Origin}' using {customerOriginStrategy.Strategy}");
        }

        cancellationToken.ThrowIfCancellationRequested();

        TrySetContentTypeForBinary(originResponse, item);

        var extension = GetFileExtension(originResponse);

        var path = GetPath(context, destinationTemplate,
            engineSettings.ImageIngest.ThrowIfNull(nameof(engineSettings.ImageIngest)));

        var targetPath = $"{path}.{extension}";

        var received = await fileSaver.SaveResponseToDisk(item, originResponse, targetPath,
            cancellationToken);

        var assetFromOrigin = context.CreateAssetFromOrigin(received, targetPath, originResponse.ContentType);

        assetFromOrigin.CustomerOriginStrategy = customerOriginStrategy;

        if (verifySize)
        {
            await VerifyFileSize(context, assetFromOrigin);
        }

        return assetFromOrigin;
    }

    private static string GetPath(IngestionContext context, string destinationTemplate, ImageIngestSettings settings)
    {
        var path = Path.Join(destinationTemplate, context.AssetId.GetDiskSafeAssetId(settings));

        if (context is AdjunctIngestionContext adjunctContext)
        {
            path = Path.Join(path, "adjuncts",
                adjunctContext.Adjunct.Id.GetDiskSafeFileId(settings));
        }

        return path;
    }

    // TODO - this may need refined depending on whether it's 'I' or 'T' ingest
    private void TrySetContentTypeForBinary(OriginResponse originResponse, IOriginItem item)
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
            var uniqueName = item.ItemId;

            var guess = GuessContentType(item.Origin!);
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
