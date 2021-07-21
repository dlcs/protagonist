using DLCS.Core.Types;
using DLCS.Model.PathElements;
using IIIF.ImageApi;
using Microsoft.AspNetCore.Http;

namespace Orchestrator.Images
{
    /// <summary>
    /// Basic model for handling iiif-img request
    /// </summary>
    public class ImageRequestModel
    {
        // TODO - make these non-nullable and throw if we can't get them.
        public string? Customer { get; private set; }
        public int? CustomerId { get; private set; }
        public string? Space { get; }
        public int SpaceId { get; }
        public string? Image { get; }
        public string? ImageRequestPath { get; }

        private ImageRequest imageRequest;
        public ImageRequest ImageRequest
        {
            get
            {
                if (imageRequest == null)
                {
                    imageRequest = ImageRequest.Parse($"{Image}/{ImageRequestPath}");
                }

                return imageRequest;
            }
        }

        public ImageRequestModel(HttpContext httpContext)
        {
            Customer = httpContext.Request.RouteValues["customer"]?.ToString();
            Space = httpContext.Request.RouteValues["space"]?.ToString();
            SpaceId = int.Parse(Space);
            Image = httpContext.Request.RouteValues["image"]?.ToString();
            ImageRequestPath = httpContext.Request.RouteValues["catchAll"]?.ToString();
        }

        public AssetImageId ToAssetImageId() => new(Customer, Space, Image);

        public void SetCustomerPathElement(CustomerPathElement customerPathElement)
        {
            Customer = customerPathElement.Name;
            CustomerId = customerPathElement.Id;
        }
    }
}