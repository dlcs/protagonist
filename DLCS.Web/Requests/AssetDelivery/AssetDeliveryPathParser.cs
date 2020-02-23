using System.Text;
using System.Threading.Tasks;
using DLCS.Model.PathElements;
using IIIF.ImageApi;

namespace DLCS.Web.Requests.AssetDelivery
{
    public class AssetDeliveryPathParser
    {
        private readonly IPathCustomerRepository pathCustomerRepository;

        public AssetDeliveryPathParser(IPathCustomerRepository pathCustomerRepository)
        {
            this.pathCustomerRepository = pathCustomerRepository;
        }

        public async Task<ThumbnailRequest> Parse(string path)
        {
            var thumbnailRequest = new ThumbnailRequest();
            await ParseBaseAssetRequest(path, thumbnailRequest);
            thumbnailRequest.IIIFImageRequest = ImageRequest.Parse(path, thumbnailRequest.BasePath);
            return thumbnailRequest;
        }

        private async Task ParseBaseAssetRequest(string path, BaseAssetRequest request)
        {
            const int customerIndex = 2;
            const int spacesIndex = 3;
            const int routeIndex = 1;
            
            string[] parts = path.Split('/');
            request.RoutePrefix = parts[routeIndex];
            request.Customer = await pathCustomerRepository.GetCustomer(parts[customerIndex]);
            request.Space = int.Parse(parts[spacesIndex]);
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
