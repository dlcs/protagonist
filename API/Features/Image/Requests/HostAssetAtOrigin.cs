using System.IO;
using System.Threading;
using System.Threading.Tasks;
using API.Settings;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Types;
using DLCS.Model.Storage;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Features.Image.Requests
{
    public class HostAssetAtOrigin : IRequest<HostAssetAtOriginResult>
    {
        public byte[] FileBytes { get; set; }
        public AssetId AssetId { get; set; }
        public string MediaType { get; set; }
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
        private readonly ApiSettings settings;

        public HostAssetAtOriginHandler(
            IBucketWriter bucketWriter,
            ILogger<HostAssetAtOriginHandler> logger,
            IOptions<ApiSettings> settings)
        {
            this.bucketWriter = bucketWriter;
            this.logger = logger;
            this.settings = settings.Value;
        }
        
        public async Task<HostAssetAtOriginResult> Handle(HostAssetAtOrigin request, CancellationToken cancellationToken)
        {
            var stream = new MemoryStream(request.FileBytes);
            
            // Save to S3
            var objectInBucket = GetObjectInBucket(request.AssetId);
            var bucketSuccess = await bucketWriter.WriteToBucket(objectInBucket, stream, request.MediaType);

            if (!bucketSuccess)
            {
                // Failed, abort
                var message = $"Failed to upload file to S3, aborting ingest. Key: '{objectInBucket}'";
                logger.LogError(message);
                return new HostAssetAtOriginResult { Error = message };
            }

            return new HostAssetAtOriginResult { Origin = objectInBucket.GetHttpUri().ToString() };
        }
        
        private RegionalisedObjectInBucket GetObjectInBucket(AssetId assetId)
            => new RegionalisedObjectInBucket(settings.AWS.S3.OriginBucket,
                assetId.ToString(), settings.AWS.Region);
    }
}