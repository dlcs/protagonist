using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.Settings;
using DLCS.Core.Settings;
using DLCS.HydraModel;
using DLCS.Model.Spaces;
using DLCS.Web.Auth;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Portal.Features.Images.Requests
{
    public class IngestSingleImage : IRequest<Image?>
    {
        public int SpaceId { get; }
        public string ImageId { get; }
        public Stream File { get; }
        public string MediaType { get; }
        
        public IngestSingleImage(int spaceId, string imageId, Stream file, string mediaType)
        {
            SpaceId = spaceId;
            ImageId = imageId;
            File = file;
            MediaType = mediaType;
        }
    }
    
    public class IngestImageFromFileHandler : IRequestHandler<IngestSingleImage, Image?>
    {
        private readonly ClaimsPrincipal claimsPrincipal;
        private readonly IBucketWriter bucketWriter;
        private readonly AWSSettings awsSettings;
        private readonly IDlcsClient dlcsClient;
        private readonly ILogger<IngestImageFromFileHandler> logger;
        private readonly ISpaceRepository spaceRepository;

        public IngestImageFromFileHandler(
            ClaimsPrincipal claimsPrincipal,
            IBucketWriter bucketWriter,
            IOptions<AWSSettings> awsSettings,
            IDlcsClient dlcsClient,
            ILogger<IngestImageFromFileHandler> logger,
            ISpaceRepository spaceRepository)
        {
            this.claimsPrincipal = claimsPrincipal;
            this.bucketWriter = bucketWriter;
            this.awsSettings = awsSettings.Value;
            this.dlcsClient = dlcsClient;
            this.logger = logger;
            this.spaceRepository = spaceRepository;
        }
        
        public async Task<Image?> Handle(IngestSingleImage request, CancellationToken cancellationToken)
        {
            // Save to S3
            var objectInBucket = GetObjectInBucket(request);
            var bucketSuccess = await bucketWriter.WriteToBucket(objectInBucket, request.File, request.MediaType);
            
            if (!bucketSuccess)
            {
                // Failed, abort
                logger.LogError("Failed to upload file to S3, aborting ingest. Key: '{Object}'",
                    objectInBucket);
                return null;
            }
            
            var asset = await CreateJsonBody(objectInBucket, request);
            var ingestResponse = await dlcsClient.DirectIngestImage(request.SpaceId, request.ImageId, asset);
            return ingestResponse;
        }

        private RegionalisedObjectInBucket GetObjectInBucket(IngestSingleImage request)
            => new RegionalisedObjectInBucket(awsSettings.S3.OriginBucket,
                $"{claimsPrincipal.GetCustomerId()}/{request.SpaceId}/{request.ImageId}", awsSettings.Region);

        private async Task<Image> CreateJsonBody(RegionalisedObjectInBucket objectInBucket, IngestSingleImage request)
        {
            var spaceCount =
                await spaceRepository.GetImageCountForSpace(claimsPrincipal.GetCustomerId().Value, request.SpaceId);
            return new Image
            {
                Origin = objectInBucket.GetHttpUri().ToString(),
                Number1 = spaceCount,
                String1 = spaceCount.ToString()
            };
        }
    }
}