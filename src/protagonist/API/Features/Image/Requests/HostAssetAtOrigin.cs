using System.IO;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Types;
using MediatR;
using Microsoft.Extensions.Logging;

namespace API.Features.Image.Requests;

/// <summary>
/// Save provided bytes to S3 origin bucket
/// </summary>
public class HostAssetAtOrigin : IRequest<HostAssetAtOriginResult>
{
    public byte[] FileBytes { get; }
    public AssetId AssetId { get; }
    public string MediaType { get; }

    public HostAssetAtOrigin(AssetId assetId, byte[] fileBytes, string mediaType)
    {
        AssetId = assetId;
        FileBytes = fileBytes;
        MediaType = mediaType;
    }
}

public class HostAssetAtOriginResult
{
    public string? Origin { get; set; }
    
    public string? Error { get; set; }
}

public class HostAssetAtOriginHandler : IRequestHandler<HostAssetAtOrigin, HostAssetAtOriginResult>
{
    private readonly IBucketWriter bucketWriter;
    private readonly ILogger<HostAssetAtOriginHandler> logger;
    private readonly IStorageKeyGenerator storageKeyGenerator;

    public HostAssetAtOriginHandler(
        IBucketWriter bucketWriter,
        ILogger<HostAssetAtOriginHandler> logger,
        IStorageKeyGenerator storageKeyGenerator)
    {
        this.bucketWriter = bucketWriter;
        this.logger = logger;
        this.storageKeyGenerator = storageKeyGenerator;
    }
    
    public async Task<HostAssetAtOriginResult> Handle(HostAssetAtOrigin request, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(request.FileBytes);
        
        // Save to S3
        var objectInBucket = storageKeyGenerator.GetAssetAtOriginLocation(request.AssetId);
        var bucketSuccess = await bucketWriter.WriteToBucket(objectInBucket, stream, request.MediaType);

        if (!bucketSuccess)
        {
            // Failed, abort
            var message = $"Failed to upload file to S3, aborting ingest. Key: '{objectInBucket}'";
            logger.LogError("Failed to upload file to S3, aborting ingest. Key: '{@ObjectInBucket}'", objectInBucket);
            return new HostAssetAtOriginResult { Error = message };
        }

        return new HostAssetAtOriginResult { Origin = objectInBucket.GetHttpUri().ToString() };
    }
}