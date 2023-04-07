#nullable disable

namespace DLCS.Repository.Entities;

/// <summary>
/// EntityCounters are used to keep track of various counts across the application
/// </summary>
public class EntityCounter
{
    /// <summary>
    /// The general type of counter (e.g. customers, customer spaces, images in space, images for customer).
    /// </summary>
    /// <remarks>See <see cref="KnownEntityCounters"/></remarks>
    public string Type { get; set; }
    
    /// <summary>
    /// Id of any additional scopes for entity-counter (e.g. for space-images this would be spaceId)
    /// </summary>
    public string Scope { get; set; }
    
    /// <summary>
    /// The next valid value for entity-counter
    /// </summary>
    public long Next { get; set; }
    
    /// <summary>
    /// The Id of customer this counter pertains to - 0 if global
    /// </summary>
    public int Customer { get; set; }
}

public static class KnownEntityCounters
{
    /// <summary>
    /// Entity counter for the number of images in a single space.
    /// </summary>
    public const string SpaceImages = "space-images";
    
    /// <summary>
    /// Entity counter for the number of images for a customer.
    /// </summary>
    public const string CustomerImages = "customer-images";
    
    /// <summary>
    /// Entity counter for the number of spaces for a customer.
    /// </summary>
    public const string CustomerSpaces = "space";
    
    /// <summary>
    /// Entity counter for the number of all customers in DLCS.
    /// </summary>
    public const string Customers = "customer";
}