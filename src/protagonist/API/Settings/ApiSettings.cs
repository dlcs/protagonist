using API.Client;
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
    
    // This will be removed from the API project once the Image refactor is done
    public ApiClientSettings API { get; set; }
    
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
}