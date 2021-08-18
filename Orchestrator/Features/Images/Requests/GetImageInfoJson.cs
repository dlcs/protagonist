using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Security;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
using MediatR;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.Mediatr;

namespace Orchestrator.Features.Images.Requests
{
    /// <summary>
    /// Mediatr request for generating info.json request for specified image.
    /// </summary>
    public class GetImageInfoJson : IRequest<ImageInfoJsonResponse>, IImageRequest
    {
        public string FullPath { get; }
        
        public ImageAssetDeliveryRequest AssetRequest { get; set; }

        public GetImageInfoJson(string path)
        {
            FullPath = path;
        }
    }

    public class ImageInfoJsonResponse
    {
        public string? InfoJson { get; private init; }
        public bool HasInfoJson { get; private init; }
        public bool RequiresAuth { get; private init; }

        public static ImageInfoJsonResponse Empty = new();

        public static ImageInfoJsonResponse Open(string infoJson) 
            => new()
            {
                InfoJson = infoJson,
                RequiresAuth = false,
                HasInfoJson = true
            };

        public static ImageInfoJsonResponse Restricted(string infoJson) 
            => new()
            {
                InfoJson = infoJson,
                RequiresAuth = true,
                HasInfoJson = true
            };
    }

    public class GetImageInfoJsonHandler : IRequestHandler<GetImageInfoJson, ImageInfoJsonResponse>
    {
        private readonly IAssetTracker assetTracker;
        private readonly IAssetPathGenerator assetPathGenerator;
        private readonly IAssetRepository assetRepository;
        private readonly IAuthServicesRepository authServicesRepository;
        private readonly IConfiguration configuration;

        public GetImageInfoJsonHandler(
            IAssetTracker assetTracker,
            IAssetPathGenerator assetPathGenerator,
            IAssetRepository assetRepository,
            IAuthServicesRepository authServicesRepository,
            IConfiguration configuration)
        {
            this.assetTracker = assetTracker;
            this.assetPathGenerator = assetPathGenerator;
            this.assetRepository = assetRepository;
            this.authServicesRepository = authServicesRepository;
            this.configuration = configuration;
        }
        
        public async Task<ImageInfoJsonResponse> Handle(GetImageInfoJson request, CancellationToken cancellationToken)
        {
            var assetId = request.AssetRequest.GetAssetId();
            var asset = await assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId);
            if (asset == null)
            {
                return ImageInfoJsonResponse.Empty;
            }

            var imageId = GetImageId(request);

            if (!asset.RequiresAuth)
            {
                var infoJson = InfoJsonBuilder.GetImageApi2_1Level1(imageId, asset.Width, asset.Height, asset.OpenThumbs);
                return ImageInfoJsonResponse.Open(infoJson);
            }
            
            var authInfoJson = await GetAuthInfoJson(imageId, asset, assetId);
            return ImageInfoJsonResponse.Restricted(authInfoJson);
        }

        private string GetImageId(GetImageInfoJson request)
            => assetPathGenerator.GetFullPathForRequest(request.AssetRequest,
                (assetRequest, template) => DlcsPathHelpers.GeneratePathFromTemplate(
                    template,
                    assetRequest.RoutePrefix,
                    assetRequest.CustomerPathValue,
                    assetRequest.Space.ToString(),
                    assetRequest.AssetId));

        private async Task<string> GetAuthInfoJson(string imageId, OrchestrationImage asset, AssetId assetId)
        {
            var authServices = await GetAuthServices(assetId);
            var infoJsonServices = GenerateInfoJsonServices(assetId, authServices);
            return InfoJsonBuilder.GetImageApi2_1Level1Auth(imageId, asset.Width, asset.Height, asset.OpenThumbs,
                infoJsonServices);
        }

        private string GenerateInfoJsonServices(AssetId assetId, List<AuthService>? authServices)
        {
            // TODO - fix this with IIIF nuget lib, this is lift + shift from Deliverator
            var authServicesUriFormat = configuration["authServicesUriTemplate"];
            var id = authServicesUriFormat
                .Replace("{customer}", assetId.Customer.ToString()) // should this be customer Path value?
                .Replace("{behaviour}", authServices[0].Name);
            var presentationObject = new JObject
            {
                { "@id", id },
                { "profile", authServices[0].Profile },
                { "label", authServices[0].Label },
                { "description", authServices[0].Description }
            };

            if (authServices.Count > 1)
            {
                presentationObject["service"] = new JArray(
                    new JObject
                    {
                        { "@id", string.Concat(presentationObject["@id"], "/", authServices[1].Name) },
                        { "profile", authServices[1].Profile }
                    },
                    new JObject
                    {
                        {
                            "@id", authServicesUriFormat
                                .Replace("{customer}", assetId.Customer.ToString())
                                .Replace("{behaviour}", "token")
                        },
                        { "profile", "http://iiif.io/api/auth/0/token" }
                    });
            }

            return presentationObject.ToString(Formatting.None);
        }

        private async Task<List<AuthService>> GetAuthServices(AssetId assetId)
        {
            var asset = await assetRepository.GetAsset(assetId);

            var authServices = new List<AuthService>();
            foreach (var role in asset.RolesList)
            {
                authServices.AddRange(await authServicesRepository.GetAuthServiceForRole(assetId.Customer, role));
            }

            return authServices;
        }
    }
}