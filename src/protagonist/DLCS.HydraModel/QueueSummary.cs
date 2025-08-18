using System;
using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

[HydraClass(typeof(QueueClass),
    Description = "A summary of overall queue counts across all customers",
    UriTemplate = "/queue")]
public class QueueSummary : DlcsResource
{
    public QueueSummary(string baseUrl)
    {
        Init(baseUrl, false);
    }
    
    [RdfProperty(Description = "Number of total images waiting to be processed.",
        Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 11, PropertyName = "incoming")]
    public int Incoming { get; set; }
    
    [RdfProperty(Description = "Number of total images waiting to be processed in priority queue.",
        Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 12, PropertyName = "priority")]
    public int Priority { get; set; }
    
    [RdfProperty(Description = "Number of total timebased assets waiting to be processed.",
        Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 13, PropertyName = "timebased")]
    public int Timebased { get; set; }
    
    [RdfProperty(Description = "Number of total timebased assets that have been transcoded and are waiting for final processing.",
        Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 14, PropertyName = "transcodeComplete")]
    public int TranscodeComplete { get; set; }
    
    [RdfProperty(Description = "Number of total file assets waiting to be processed.",
        Range = Names.XmlSchema.NonNegativeInteger, ReadOnly = true, WriteOnly = false)]
    [JsonProperty(Order = 15, PropertyName = "file")]
    public int File { get; set; }
    
    [Obsolete("For backwards compat with Deliverator but not used")]
    [JsonProperty(Order = 20, PropertyName = "failed")]
    public int Failed { get; set; }
    
    [Obsolete("For backwards compat with Deliverator but not used")]
    [JsonProperty(Order = 21, PropertyName = "success")]
    public int Success { get; set; }
}

public class QueueSummaryClass : Class
{
    public QueueSummaryClass()
    {
        BootstrapViaReflection(typeof(QueueSummaryClass));
    }

    public override void DefineOperations()
    {
        // no-op
    }
}