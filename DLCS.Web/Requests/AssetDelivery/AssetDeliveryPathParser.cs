using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DLCS.Model.PathElements;
using IIIF.ImageApi;
using Microsoft.AspNetCore.Http;

namespace DLCS.Web.Requests.AssetDelivery
{
    public class AssetDeliveryPathParser
    {
        private readonly IPathCustomerRepository pathCustomerRepository;

        public AssetDeliveryPathParser(IPathCustomerRepository pathCustomerRepository)
        {
            this.pathCustomerRepository = pathCustomerRepository;
        }

        public async Task<AssetDeliveryRequest> Parse(string path)
        {
            var assetDeliveryRequest = new AssetDeliveryRequest();
            await ParseBaseAssetRequest(path, assetDeliveryRequest);
            assetDeliveryRequest.IIIFImageRequest = ImageRequest.Parse(path, assetDeliveryRequest.BasePath);
            return assetDeliveryRequest;
        }

        private async Task ParseBaseAssetRequest(string path, BaseAssetRequest request)
        {
            const int routeIndex = 0;
            const int customerIndex = 1;
            const int spacesIndex = 2;
            
            string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            request.RoutePrefix = parts[routeIndex];
            request.CustomerPathValue = parts[customerIndex];
            request.Customer = await pathCustomerRepository.GetCustomer(parts[customerIndex]);
            request.Space = int.Parse(parts[spacesIndex]);
            request.AssetPath = string.Join("/", parts.Skip(3));
            request.AssetId = request.AssetPath.Split("/")[0];
            request.BasePath = new StringBuilder("/", 50)
                .Append(parts[routeIndex])
                .Append("/")
                .Append(parts[customerIndex])
                .Append("/")
                .Append(parts[spacesIndex])
                .Append("/")
                .ToString();
        }
    }
}
