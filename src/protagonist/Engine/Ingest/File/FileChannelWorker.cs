using DLCS.Model.Assets;
using DLCS.Model.Customers;
using Engine.Ingest.Persistence;

namespace Engine.Ingest.File;

public class FileChannelWorker : IAssetIngesterWorker
{
    private readonly IAssetToS3 assetToS3;

    public FileChannelWorker(IAssetToS3 assetToS3)
    {
        this.assetToS3 = assetToS3;
    }
    
    public async Task<IngestResultStatus> Ingest(IngestionContext ingestionContext,
        CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default)
    {
        if (ingestionContext.Asset.HasDeliveryChannel(AssetDeliveryChannels.File))
        {
            // S3 to S3 copy
            // Check storage limits
            // method like - assetToS3.CopyAssetToTranscodeInput()
            // May need to be called last of all executors, and aware of whether Image was uploaded to S3.
            // Update ImageStorage value if storing
            // Also, aware of Optimised origin.
        }
        else
        {
            // Delete the possible S3 location, knowing it might be a noop
        }

        throw new NotImplementedException();
    }
}