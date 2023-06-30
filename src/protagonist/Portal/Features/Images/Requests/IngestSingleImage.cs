using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Types;
using DLCS.HydraModel;
using DLCS.Web.Auth;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Portal.Features.Images.Requests;

public class IngestSingleImage : IRequest<Image?>
{
    public int SpaceId { get; }
    public string ImageId { get; }
    public Stream UploadedFileStream { get; }
    public string MediaType { get; }
    public int ImageIndex { get; }
    
    public IngestSingleImage(int spaceId, string imageId, Stream uploadedFileStream, string mediaType, int imageIndex)
    {
        SpaceId = spaceId;
        ImageId = imageId;
        UploadedFileStream = uploadedFileStream;
        MediaType = mediaType;
        ImageIndex = imageIndex;
    }
}

public class IngestImageFromFileHandler : IRequestHandler<IngestSingleImage, Image?>
{
    private const string StandardMediaType = "image/jpeg";
    
    private readonly ClaimsPrincipal claimsPrincipal;
    private readonly IBucketWriter bucketWriter;
    private readonly IDlcsClient dlcsClient;
    private readonly ILogger<IngestImageFromFileHandler> logger;
    private readonly IStorageKeyGenerator storageKeyGenerator;

    public IngestImageFromFileHandler(
        ClaimsPrincipal claimsPrincipal,
        IBucketWriter bucketWriter,
        IDlcsClient dlcsClient,
        ILogger<IngestImageFromFileHandler> logger,
        IStorageKeyGenerator storageKeyGenerator)
    {
        this.claimsPrincipal = claimsPrincipal;
        this.bucketWriter = bucketWriter;
        this.dlcsClient = dlcsClient;
        this.logger = logger;
        this.storageKeyGenerator = storageKeyGenerator;
    }
    
    public async Task<Image?> Handle(IngestSingleImage request, CancellationToken cancellationToken)
    {
        // Save to S3
        var assetId = new AssetId(claimsPrincipal.GetCustomerId()!.Value, request.SpaceId, request.ImageId);
        var objectInBucket = storageKeyGenerator.GetAssetAtOriginLocation(assetId);
        var bucketSuccess = await bucketWriter.WriteToBucket(objectInBucket, request.UploadedFileStream, request.MediaType);
        
        if (!bucketSuccess)
        {
            // Failed, abort
            logger.LogError("Failed to upload file to S3, aborting ingest. Key: '{Object}'",
                objectInBucket);
            return null;
        }
        
        var asset = CreateJsonBody(objectInBucket, request);
        var ingestResponse = await dlcsClient.DirectIngestImage(request.SpaceId, request.ImageId, asset);
        return ingestResponse;
    }

    private Image CreateJsonBody(RegionalisedObjectInBucket objectInBucket, IngestSingleImage request)
    {
        return new Image
        {
            Origin = objectInBucket.GetHttpUri().ToString(),
            MediaType = StandardMediaType,
            Number1 = request.ImageIndex,
            String1 = request.ImageIndex.ToString()
        };
    }
}