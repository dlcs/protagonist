using System.Collections.Generic;

namespace API.Settings;

public class CustomerOverrideSettings
{
    public static readonly CustomerOverrideSettings Empty = new();
    
    /// <summary>
    /// Whether the customer has legacy support enabled
    /// </summary>
    public bool LegacySupport { get; init; } = false;

    /// <summary>
    /// Spaces which are exempt from legacy support in a customer that has legacy support enabled
    /// </summary>
    public List<string> NovelSpaces { get; init; } = new();
}