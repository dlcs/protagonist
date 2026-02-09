using System.Collections.Generic;
using DLCS.AWS.Settings;
using DLCS.Core.Settings;

namespace API.Settings;

public class ApiSettings
{
    private char[] restrictedResourceIdCharacters = [];
    private string restrictedResourceIdCharacterString = string.Empty;

    public DlcsSettings DLCS { get; set; } = null!;
    
    public AWSSettings AWS { get; set; } = null!;

    public string? PathBase { get; set; }

    public string ApiSalt { get; set; } = null!;

    public string LoginSalt { get; set; } = null!;

    /// <summary>
    /// The system maximum width value. Images cannot be registered with a maxWidth that exceeds this.
    /// </summary>
    public int MaxWidth { get; set; } = SystemDefaults.MaxWidth;
    
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
    /// Characters that are not allowed in resource ids (e.g. Adjunct and Asset)
    /// </summary>
    public char[] RestrictedResourceIdCharacters => restrictedResourceIdCharacters;

    /// <summary>
    /// A string of characters not allowed in resource identifiers (e.g. Adjunct and Asset)
    /// </summary>
    public string RestrictedResourceIdCharacterString
    {
        get => restrictedResourceIdCharacterString;
        set
        {
            restrictedResourceIdCharacterString = value;
            restrictedResourceIdCharacters = restrictedResourceIdCharacterString.ToCharArray();
        }
    }

    /// <summary>
    /// Check if the provided resourceId contains invalid characters
    /// </summary>
    public bool DoesResourceIdContainRestrictedCharacters(string? resourceId)
        => !string.IsNullOrEmpty(resourceId) && resourceId.IndexOfAny(RestrictedResourceIdCharacters) != -1;
}
