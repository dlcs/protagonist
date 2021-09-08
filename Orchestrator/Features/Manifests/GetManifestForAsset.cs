using System.Threading;
using System.Threading.Tasks;
using DLCS.Web.Requests.AssetDelivery;
using MediatR;
using Orchestrator.Infrastructure.Mediatr;
using Orchestrator.Models;

namespace Orchestrator.Features.Manifests
{
    /// <summary>
    /// Mediatr request for generating basic single-item manifest for specified image
    /// </summary>
    public class GetManifestForAsset : IRequest<IIIFJsonResponse>, IGenericAssetRequest
    {
        public string FullPath { get; }
        
        public BaseAssetRequest AssetRequest { get; set; }

        public GetManifestForAsset(string path)
        {
            FullPath = path;
        }
    }
    
    public class GetManifestForAssetHandler : IRequestHandler<GetManifestForAsset, IIIFJsonResponse>
    {
        public Task<IIIFJsonResponse> Handle(GetManifestForAsset request, CancellationToken cancellationToken)
        {
            return Task.FromResult(IIIFJsonResponse.Empty);
        }
    }
}