using DLCS.Core.Types;
using DLCS.Model.PathElements;

namespace DLCS.Web.Requests.AssetDelivery
{
    public class BaseAssetRequest : IBasicPathElements
    {
        /// <summary>
        /// The request root, e.g. "thumbs", "iiif-img" etc.
        /// </summary>
        public string RoutePrefix { get; set; }
        
        /// <summary>
        /// The request root, including Version, if available.
        /// e.g. "thumbs", "iiif-img", "iiif-img/v2", "thumbs/v3" etc.
        /// </summary>
        public string VersionedRoutePrefix { get; set; }
        
        /// <summary>
        /// The requested version slug, if present. e.g. v2, v3 etc.
        /// </summary>
        public string? VersionPathValue { get; set; }
        
        /// <summary>
        /// The customer for this request.
        /// </summary>
        public CustomerPathElement Customer { get; set; }
        
        /// <summary>
        /// The "customer" value from request (int or string value). 
        /// </summary>
        public string CustomerPathValue { get; set; }
        
        /// <summary>
        /// The Space for this request.
        /// </summary>
        public int Space { get; set; }
        
        /// <summary>
        /// The BasePath for this request, this is {routePrefix}/{customer}/{space}
        /// </summary>
        public string BasePath { get; set; }
        
        /// <summary>
        /// The AssetPath for this request, this is everything after space. e.g.
        /// my-image/full/61,100/0/default.jpg
        /// my-audio/full/full/max/max/0/default.mp4
        /// file-identifier
        /// </summary>
        public string AssetPath { get; set; }

        /// <summary>
        /// The Id of the asset in the request
        /// e.g. my-image/full/61,100/0/default.jpg => my-image
        /// </summary>
        public string AssetId { get; set; }
        
        /// <summary>
        /// The normalised BasePath for this request, this is {routePrefix}/{customer}/{space} always using numeric
        /// value for {customer}, regardless of what was passed 
        /// </summary>
        public string NormalisedBasePath { get; set; }
        
        /// <summary>
        /// The normalised original full request Path, always using numeric  value for {customer}, regardless of what
        /// was passed. 
        /// </summary>
        public string NormalisedFullPath { get; set; }

        /// <summary>
        /// Generate an <see cref="Core.Types.AssetId"/> object from BaseAssetRequest
        /// </summary>
        public AssetId GetAssetId() => new(Customer.Id, Space, AssetId);
    }
}
