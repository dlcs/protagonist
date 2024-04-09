namespace Engine.Ingest.Image.ImageServer.Clients;

public interface ICantaloupeThumbsClient
{
    /// <summary>
    /// Calls cantaloupe for thumbs
    /// </summary>
    /// <param name="context">The context of the request</param>
    /// <param name="thumbSizes">A list of thumbnail sizes to generate</param>
    /// <param name="thumbFolder">Root folder for saving thumbs</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A list of images on disk</returns>
    public Task<List<ImageOnDisk>> GenerateThumbnails(IngestionContext context,
        List<string> thumbSizes, string thumbFolder, CancellationToken cancellationToken = default);
}