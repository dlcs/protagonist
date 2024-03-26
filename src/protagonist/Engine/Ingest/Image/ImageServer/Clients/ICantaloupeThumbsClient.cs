using DLCS.Core.Types;

namespace Engine.Ingest.Image.ImageServer.Clients;

public interface ICantaloupeThumbsClient
{
    public Task<List<ImageOnDisk>> CallCantaloupe(IngestionContext context, AssetId modifiedAssetId,
        List<string> thumbSizes, CancellationToken cancellationToken = default);
}