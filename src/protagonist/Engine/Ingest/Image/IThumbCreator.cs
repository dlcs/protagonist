using DLCS.Model.Assets;

namespace Engine.Ingest.Image;

public interface IThumbCreator
{
    /// <summary>
    /// Create new thumbs in S3 from provided images on disk
    /// </summary>
    /// <param name="asset">Asset thumbnails are for</param>
    /// <param name="thumbsToProcess">List of jpgs on disk that are to be copied to S3</param>
    /// <returns></returns>
    Task<int> CreateNewThumbs(Asset asset, IReadOnlyList<ImageOnDisk> thumbsToProcess);
}