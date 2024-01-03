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
    
    public string ApiSalt { get; set; }
    
    public string LoginSalt { get; set; }
    
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
    public bool DefaultLegacySupport { get; set; }
    
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
    
    /// <summary>
    /// Get whether legacy mode is enabled for a particular customer and space
    /// </summary>
    /// <param name="customerId">CustomerId to get settings for.</param>
    /// <param name="spaceId">The space to check if legacy mode is disabled</param>
    /// <returns>Whether legacy mode is enabled or not</returns>
    public bool LegacyModeEnabledForSpace(int customerId, int spaceId)
        => CustomerOverrides.TryGetValue(Convert.ToString(customerId), out var settings) 
            ? settings.LegacySupport && !settings.NovelSpaces.Contains(spaceId.ToString()) 
            : DefaultLegacySupport;
    
    /// <summary>
    /// Get whether legacy mode is enabled for a particular customer
    /// </summary>
    /// <param name="customerId">CustomerId to get settings for.</param>
    /// <returns>Whether legacy mode is enabled or not</returns>
    public bool LegacyModeEnabledForCustomer(int customerId)
        => CustomerOverrides.TryGetValue(Convert.ToString(customerId), out var settings) 
            ? settings.LegacySupport 
            : DefaultLegacySupport;
    
    /// <summary>
    /// Whether the delivery channel feature is enabled
    /// </summary>
    public bool DeliveryChannelsEnabled { get; set; }

    /// <summary>
    /// Characters that are not allowed in an asset id
    /// </summary>
    public string RestrictedAssetIdCharacters { get; set; } = "";
}