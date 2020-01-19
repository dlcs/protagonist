using System;
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

        public ThumbnailRequest Parse(string path)
        {
            var thumbnailRequest = new ThumbnailRequest();
            ParseBaseAssetRequest(path, thumbnailRequest);
            thumbnailRequest.IIIFImageRequest = ImageRequest.Parse(path, thumbnailRequest.BasePath);
            return thumbnailRequest;
        }

        private void ParseBaseAssetRequest(string path, BaseAssetRequest request)
        {
            string[] parts = path.Split('/');
            request.RoutePrefix = parts[1];
            request.Customer = pathCustomerRepository.GetCustomer(parts[2]);
            request.Space = Int32.Parse(parts[3]);
            request.BasePath = $"/{parts[1]}/{parts[2]}/{parts[3]}/"; // is that the fastest way?
        }
    }
}
