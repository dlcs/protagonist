#nullable disable

namespace DLCS.Repository.Entities;

public partial class MetricThreshold
{
    public string Name { get; set; }
    public string Metric { get; set; }
    public long? Lower { get; set; }
    public long? Upper { get; set; }
}
