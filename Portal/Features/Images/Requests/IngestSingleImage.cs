using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Settings;
using DLCS.HydraModel;
using DLCS.Repository.Spaces;
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
        private readonly IBucketReader bucketReader;
        private readonly DlcsSettings settings;
        private readonly IDlcsClient dlcsClient;
        private readonly ILogger<IngestImageFromFileHandler> logger;
        private readonly ISpaceRepository spaceRepository;

        public IngestImageFromFileHandler(
            ClaimsPrincipal claimsPrincipal,
            IBucketReader bucketReader,
            IOptions<DlcsSettings> settings,
            IDlcsClient dlcsClient,
            ILogger<IngestImageFromFileHandler> logger,
            ISpaceRepository spaceRepository)
        {
            this.claimsPrincipal = claimsPrincipal;
            this.bucketReader = bucketReader;
            this.settings = settings.Value;
            this.dlcsClient = dlcsClient;
            this.logger = logger;
            this.spaceRepository = spaceRepository;
        }
        
        public async Task<Image?> Handle(IngestSingleImage request, CancellationToken cancellationToken)
        {
            // Save to S3
            var objectInBucket = GetObjectInBucket(request);
            var bucketSuccess = await bucketReader.WriteToBucket(objectInBucket, request.File, request.MediaType);
            
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
            => new RegionalisedObjectInBucket(settings.OriginBucket,
                $"{claimsPrincipal.GetCustomerId()}/{request.SpaceId}/{request.ImageId}", settings.Region);

        private async Task<Image> CreateJsonBody(RegionalisedObjectInBucket objectInBucket, IngestSingleImage request)
        {
            var spaceCount =
                await spaceRepository.GetImageCountForSpace(claimsPrincipal.GetCustomerId().Value, request.SpaceId);
            return new Image
            {
                Origin = objectInBucket.GetHttpUri(),
                Number1 = spaceCount,
                String1 = spaceCount.ToString()
            };
        }
    }
}