namespace DLCS.Web.Requests.AssetDelivery;

public class BasicPathElements : IBasicPathElements
{
    public string RoutePrefix { get; set; }
    public string? VersionPathValue { get; set; }
    public string CustomerPathValue { get; set; }
    public int Space { get; set; }
    public string AssetPath { get; set; }
}