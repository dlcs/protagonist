using DLCS.Core.Types;

namespace Engine.Ingest.Image.ImageServer.Clients;

public interface ICantaloupeThumbsClient
{
    /// <summary>
    /// Calls cantaloupe for thumbs
    /// </summary>
    /// <param name="context">The context of the request</param>
    /// <param name="thumbSizes">A list of thumbnail sizes to generate</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A list of images on disk</returns>
    public Task<List<ImageOnDisk>> CallCantaloupe(IngestionContext context,
        List<string> thumbSizes, CancellationToken cancellationToken = default);
}