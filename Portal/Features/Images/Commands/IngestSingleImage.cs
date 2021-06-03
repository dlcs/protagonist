using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Features.Image.Models;
using DLCS.Core.Settings;
using DLCS.Model.Storage;
using DLCS.Repository.Spaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portal.Legacy;

namespace Portal.Features.Images.Commands
{
    public class IngestSingleImage : IRequest<AssetJsonLD?>
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
    
    public class IngestImageFromFileHandler : IRequestHandler<IngestSingleImage, AssetJsonLD?>
    {
        private readonly ClaimsPrincipal claimsPrincipal;
        private readonly IBucketReader bucketReader;
        private readonly DlcsSettings settings;
        private readonly DlcsClient dlcsClient;
        private readonly ILogger<IngestImageFromFileHandler> logger;
        private readonly ISpaceRepository spaceRepository;

        public IngestImageFromFileHandler(
            ClaimsPrincipal claimsPrincipal,
            IBucketReader bucketReader,
            IOptions<DlcsSettings> settings,
            DlcsClient dlcsClient,
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
        
        public async Task<AssetJsonLD?> Handle(IngestSingleImage request, CancellationToken cancellationToken)
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

        private async Task<AssetJsonLD> CreateJsonBody(RegionalisedObjectInBucket objectInBucket, IngestSingleImage request)
        {
            var spaceCount =
                await spaceRepository.GetImageCountForSpace(claimsPrincipal.GetCustomerId().Value, request.SpaceId);
            var nextCount = (spaceCount ?? 0) + 1;
            return new AssetJsonLD
            {
                Origin = objectInBucket.GetHttpUri(),
                Number1 = nextCount,
                String1 = nextCount.ToString()
            };
        }
    }
}