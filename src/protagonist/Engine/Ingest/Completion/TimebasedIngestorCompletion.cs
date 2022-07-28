using DLCS.Core.Types;
using DLCS.Model.Assets;
using Engine.Ingest.Timebased;

namespace Engine.Ingest.Completion;

public class TimebasedIngestorCompletion : ITimebasedIngestorCompletion
{
    private readonly IEngineAssetRepository assetRepository;
    private readonly ILogger<TimebasedIngestorCompletion> logger;

    public TimebasedIngestorCompletion(
        IEngineAssetRepository assetRepository,
        ILogger<TimebasedIngestorCompletion> logger)
    {
        this.assetRepository = assetRepository;
        this.logger = logger;
    }

    public async Task<bool> CompleteSuccessfulIngest(AssetId assetId, TranscodeResult transcodeResult,
        CancellationToken cancellationToken = default)
    {
        // TODO - do we want to attempt to set the mediaType here; based on ContentType of origin?

        var asset = await assetRepository.GetAsset(assetId, cancellationToken);

        if (asset == null)
        {
            logger.LogError("Unable to find asset {AssetId} in database", assetId);
            return false;
        }
        
        // CompleteAssetInDatabase
        // Move items from ET output to storage bucket
        // Remove ET input file

        throw new NotImplementedException();
    }

    public async Task<bool> CompleteAssetInDatabase(Asset asset, long? assetSize = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ImageStorage? imageStore = null;
            ImageLocation? imageLocation = null;
            if (assetSize.HasValue)
            {
                imageStore = new ImageStorage
                {
                    Id = asset.Id,
                    Customer = asset.Customer,
                    Space = asset.Space,
                    LastChecked = DateTime.UtcNow,
                    Size = assetSize.Value
                };

                // NOTE - ImageLocation isn't used for 'T', only 'I' family so just set an empty record
                imageLocation = new ImageLocation { Id = asset.Id, Nas = string.Empty, S3 = string.Empty };
            }

            var success =
                await assetRepository.UpdateIngestedAsset(asset, imageLocation, imageStore, cancellationToken);
            return success;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error marking AV asset as completed '{AssetId}'", asset.Id);
            return false;
        }
    }
}