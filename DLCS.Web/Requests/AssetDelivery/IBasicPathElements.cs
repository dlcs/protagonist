namespace DLCS.Web.Requests.AssetDelivery
{
    /// <summary>
    /// Interface containing basic required elements for generating URLs
    /// </summary>
    public interface IBasicPathElements
    {
        /// <summary>
        /// The request root, e.g. "thumbs", "iiif-img" etc.
        /// </summary>
        public string RoutePrefix { get; }
        
        /// <summary>
        /// The "customer" value from request (int or string value). 
        /// </summary>
        public string CustomerPathValue { get; }
        
        /// <summary>
        /// The Space for this request.
        /// </summary>
        public int Space { get; }
        
        /// <summary>
        /// The AssetPath for this request, this is everything after space. e.g.
        /// my-image/full/61,100/0/default.jpg
        /// my-audio/full/full/max/max/0/default.mp4
        /// file-identifier
        /// </summary>
        public string AssetPath { get; }
    }
}