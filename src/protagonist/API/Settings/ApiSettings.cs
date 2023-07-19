using System.Collections.Generic;
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

    /// <summary>
    /// Whether legacy support is enabled by default
    /// </summary>
    public bool DefaultLegacySupport { get; set; } = true;
    
    /// <summary>
    /// A collection of customer-specific overrides, keyed by customerId.
    /// </summary> 
    // ReSharper disable once CollectionNeverUpdated.Global
    public Dictionary<string, CustomerOverrideSettings> CustomerOverrides { get; set; } = new();

    /// <summary>
    /// Get CustomerSpecificSettings, if found. 
    /// </summary>
    /// <param name="customerId">CustomerId to get settings for.</param>
    /// <returns>Customer specific overrides, or default if not found.</returns>
    public CustomerOverrideSettings GetCustomerSettings(int customerId)
        => CustomerOverrides.TryGetValue(customerId.ToString(), out var settings)
            ? settings
            : new CustomerOverrideSettings
            {
                LegacySupport = DefaultLegacySupport
            };
}