#nullable disable

using System.Diagnostics;

namespace DLCS.Model.Customers;

/// <summary>
/// CustomerOriginStrategy controls how the DLCS fetches images from Origin
/// </summary>
[DebuggerDisplay("Cust:{Customer}, {Strategy} - {Regex}")]
public class CustomerOriginStrategy
{
    /// <summary>
    /// Unique identifier, random guid
    /// </summary>
    public string Id { get; set; }
    
    /// <summary>
    /// Customer Id
    /// </summary>
    public int Customer { get; set; }
    
    /// <summary>
    /// Regex used against Asset Origin to determine if this is a matching strategy 
    /// </summary>
    public string Regex { get; set; }
    
    /// <summary>
    /// How the Asset should be fetched from origin 
    /// </summary>
    public OriginStrategyType Strategy { get; set; }
    
    /// <summary>
    /// Optional credentials used for some <see cref="OriginStrategyType"/>
    /// </summary>
    public string Credentials { get; set; } = string.Empty;
    
    /// <summary>
    /// Signifies that this is fast and stable enough to be treated like DLCS' own storage
    /// </summary>
    public bool Optimised { get; set; }
    
    /// <summary>
    /// Allows control over the order in which CustomerOriginStrategies are checked.
    /// Lower numbers are checked first, first matching record is chosen. 
    /// </summary>
    public int Order { get; set; }
}
