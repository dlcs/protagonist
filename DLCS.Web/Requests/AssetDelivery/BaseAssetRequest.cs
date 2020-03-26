using DLCS.Model.PathElements;

namespace DLCS.Web.Requests.AssetDelivery
{
    public class BaseAssetRequest
    {
        public string RoutePrefix { get; set; }
        public CustomerPathElement Customer { get; set; }
        public int Space { get; set; }
        public string BasePath { get; set; }
    }
}
