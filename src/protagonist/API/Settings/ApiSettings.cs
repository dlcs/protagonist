using DLCS.AWS.Settings;
using DLCS.Core.Settings;

namespace API.Settings;

public class ApiSettings
{
    /// <summary>
    /// The base URI of DLCS to hand-off requests to.
    /// </summary>
    public DlcsSettings DLCS { get; set; }
    
    public AWSSettings AWS { get; set; }

    public string PathBase { get; set; }
    
    public string Salt { get; set; }
    
    /// <summary>
    /// The default PageSize for endpoints that support paging 
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// The maximum number of images that can be POSTed in a single batch
    /// </summary>
    public int MaxBatchSize { get; set; } = 250;
    
    /// <summary>
    /// The maximum number of images that can be POSTed in a single batch
    /// </summary>
    public int MaxImageListSize { get; set; } = 500;
}