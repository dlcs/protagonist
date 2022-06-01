using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using API.Settings;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core;
using DLCS.Web.Requests;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace API.Features.Image.Requests
{
    /// <summary>
    /// Request object to ingest image directly from a file.
    /// </summary>
    public class IngestImageFromFile : IRequest<ResultStatus<DelegatedIngestResponse>>
    {
        // NOTE - these 3 will often come together, maybe have something to handle as a group
        public string CustomerId { get; }
        public string SpaceId { get; }
        public string ImageId { get; }
        public Stream File { get; }
        
        public DLCS.HydraModel.Image Body { get; }
        
        // TODO - temporary as we forward this on from those the user sent
        public AuthenticationHeaderValue? BasicAuth { get; }

        public override string ToString() => $"{CustomerId}/{SpaceId}/{ImageId}";

        public IngestImageFromFile(string customerId, string spaceId, string imageId, Stream file, DLCS.HydraModel.Image body,
            AuthenticationHeaderValue? basicAuth)
        {
            CustomerId = customerId;
            SpaceId = spaceId;
            ImageId = imageId;
            File = file;
            Body = body;
            BasicAuth = basicAuth;
        }
    }
    
    /// <summary>
    /// Handler for direct ingesting image by delegating logic to dlcs API.
    /// </summary>
    public class IngestImageFromFileHandler : IRequestHandler<IngestImageFromFile, ResultStatus<DelegatedIngestResponse>>
    {
        private readonly IBucketWriter bucketWriter;
        private readonly IHttpClientFactory clientFactory;
        private readonly ILogger<IngestImageFromFileHandler> logger;
        private readonly ApiSettings settings;

        public IngestImageFromFileHandler(
            IBucketWriter bucketWriter, 
            IOptions<ApiSettings> settings,
            IHttpClientFactory clientFactory,
            ILogger<IngestImageFromFileHandler> logger)
        {
            this.bucketWriter = bucketWriter;
            this.clientFactory = clientFactory;
            this.logger = logger;
            this.settings = settings.Value;
        }

        public async Task<ResultStatus<DelegatedIngestResponse>> Handle(
            IngestImageFromFile request,
            CancellationToken cancellationToken)
        {
            // Save to S3
            var objectInBucket = GetObjectInBucket(request);
            var bucketSuccess = await bucketWriter.WriteToBucket(objectInBucket, request.File, request.Body.MediaType);

            if (!bucketSuccess)
            {
                // Failed, abort
                logger.LogError($"Failed to upload file to S3, aborting ingest. Key: '{objectInBucket}'");
                return new ResultStatus<DelegatedIngestResponse>(false);
            }

            request.Body.Origin = objectInBucket.GetHttpUri().ToString();
            var ingestResponse = await CallDlcsIngest(request, cancellationToken);
            var responseBody = await ingestResponse.Content.ReadAsStringAsync();
            var imageResult = JsonConvert.DeserializeObject<DLCS.HydraModel.Image>(responseBody);

            return ResultStatus<DelegatedIngestResponse>.Successful(new DelegatedIngestResponse(
                ingestResponse.StatusCode,
                imageResult));
        }

        private async Task<HttpResponseMessage> CallDlcsIngest(IngestImageFromFile request, CancellationToken cancellationToken)
        {
            string ingestJson = JsonConvert.SerializeObject(request.Body,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    ContractResolver = new DefaultContractResolver {NamingStrategy = new CamelCaseNamingStrategy()}
                });

            var client = clientFactory.CreateClient("dlcs-api");
            
            var requestUri = $"{settings.DLCS.ApiRoot}/customers/{request.CustomerId}/spaces/{request.SpaceId}/images/{request.ImageId}";
            
            logger.LogDebug($"Ingesting '{ingestJson}' at '{requestUri}'");

            var requestMessage = new HttpRequestMessage(HttpMethod.Put, requestUri)
            {
                Content = new StringContent(ingestJson, Encoding.UTF8, "application/json")
            };
            requestMessage.Headers.AddBasicAuth(request.BasicAuth.Parameter);
            return await client.SendAsync(requestMessage, cancellationToken);
        }

        private RegionalisedObjectInBucket GetObjectInBucket(IngestImageFromFile request)
            => new RegionalisedObjectInBucket(settings.AWS.S3.OriginBucket,
                $"{request.CustomerId}/{request.SpaceId}/{request.ImageId}", settings.AWS.Region);
    }

    public class DelegatedIngestResponse
    {
        public DLCS.HydraModel.Image? Body { get; }
        
        // NOTE - this isn't ideal but is temporary
        public HttpStatusCode? DownstreamStatusCode { get; }

        public override string ToString() => DownstreamStatusCode?.ToString() ?? "_unknown_";

        public DelegatedIngestResponse(HttpStatusCode? downstreamStatusCode, DLCS.HydraModel.Image? body)
        {
            DownstreamStatusCode = downstreamStatusCode;
            Body = body;
        }
    }
}