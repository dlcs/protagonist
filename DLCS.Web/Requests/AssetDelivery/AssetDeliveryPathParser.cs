using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DLCS.Model.PathElements;
using IIIF.ImageApi;

namespace DLCS.Web.Requests.AssetDelivery
{
    /// <summary>
    /// Contains operations for parsing asset delivery requests.
    /// </summary>
    public interface IAssetDeliveryPathParser
    {
        /// <summary>
        /// Parse asset delivery requests to <see cref="BaseAssetRequest"/>
        /// </summary>
        /// <param name="path">Full asset request path</param>
        /// <typeparam name="T">Type of <see cref="BaseAssetRequest"/> to return.</typeparam>
        /// <returns>Populated <see cref="ImageAssetDeliveryRequest"/> object</returns>
        Task<T> Parse<T>(string path) where T : BaseAssetRequest, new();
    }

    public class AssetDeliveryPathParser : IAssetDeliveryPathParser
    {
        private readonly IPathCustomerRepository pathCustomerRepository;

        public AssetDeliveryPathParser(IPathCustomerRepository pathCustomerRepository)
        {
            this.pathCustomerRepository = pathCustomerRepository;
        }

        public async Task<T> Parse<T>(string path) 
            where T : BaseAssetRequest, new()
        {
            var assetRequest = new T();
            await ParseBaseAssetRequest(path, assetRequest);

            if (assetRequest is ImageAssetDeliveryRequest imageAssetRequest)
            {
                imageAssetRequest.IIIFImageRequest = ImageRequest.Parse(path, assetRequest.BasePath);
            }
            else if (assetRequest is TimeBasedAssetDeliveryRequest timebasedAssetRequest)
            {
                timebasedAssetRequest.TimeBasedRequest = timebasedAssetRequest.AssetPath.Replace(assetRequest.AssetId, string.Empty);
            }

            return assetRequest;
        }

        private async Task ParseBaseAssetRequest(string path, BaseAssetRequest request)
        {
            const int routeIndex = 0;
            const int customerIndex = 1;
            const int spacesIndex = 2;
            
            string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            request.RoutePrefix = parts[routeIndex];
            request.CustomerPathValue = parts[customerIndex];
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
            
            // TODO - should we verify Space exists here?
            request.Customer = await pathCustomerRepository.GetCustomer(parts[customerIndex]);
        }
    }
}
