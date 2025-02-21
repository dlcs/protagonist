using System.Collections.Generic;
using DLCS.Core.Guard;

namespace DLCS.Model.Assets.NamedQueries;

/// <summary>
/// This class represents the results of parsing NQ template with specified arguments, specified in the template
/// and via URL when making request
/// </summary>
public class ParsedNamedQuery
{
    /// <summary>
    /// Collection of OrderBy clauses to apply to assets
    /// </summary>
    public List<QueryOrder> AssetOrdering { get; set; } = new() { new QueryOrder(QueryMapping.Unset) };
    
    /// <summary>
    /// Value of "space" parameter after parsing
    /// </summary>
    public int? Space { get; set; }
    
    /// <summary>
    /// Value of "spacename" parameter after parsing
    /// </summary>
    public string? SpaceName { get; set; }
    
    /// <summary>
    /// Value of "s1" parameter after parsing
    /// </summary>
    public string? String1 { get; set; }
    
    /// <summary>
    /// Value of "s2" parameter after parsing
    /// </summary>
    public string? String2 { get; set; }
    
    /// <summary>
    /// Value of "s3" parameter after parsing
    /// </summary>
    public string? String3 { get; set; }

    /// <summary>
    /// Value of "n1" parameter after parsing
    /// </summary>
    public long? Number1 { get; set; }
    
    /// <summary>
    /// Value of "n2" parameter after parsing
    /// </summary>
    public long? Number2 { get; set; }
    
    /// <summary>
    /// Value of "n3" parameter after parsing
    /// </summary>
    public long? Number3 { get; set; }
    
    /// <summary>
    /// Value of "batches" after parsing
    /// </summary>
    public int[]? Batches { get; set; } 
    
    /// <summary>
    /// The name of the namedQuery this object was parsed from.
    /// </summary>
    public string NamedQueryName { get; set; }

    /// <summary>
    /// CustomerId associated with request
    /// </summary>
    public int Customer { get; set; }
    
    /// <summary>
    /// Whether the NQ could be parsed correctly
    /// </summary>
    public bool IsFaulty { get; private set; }
    
    /// <summary>
    /// Any error message associated with this NQ, will have value if <see cref="IsFaulty"/> is true.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    public ParsedNamedQuery(int customerId)
    {
        Customer = customerId;
    }

    /// <summary>
    /// Mark this result as containing errors
    /// </summary>
    public void SetError(string errorMessage)
    {
        ErrorMessage = errorMessage.ThrowIfNullOrWhiteSpace(nameof(errorMessage));
        IsFaulty = true;
    }
    
    /// <summary>
    /// Enum specifying the source of data for a NQ property
    /// </summary>
    public enum QueryMapping
    {
        Unset,
        String1,
        String2,
        String3,
        Number1,
        Number2,
        Number3,
    }

    public enum OrderDirection
    {
        Ascending,
        Descending
    }

    /// <summary>
    /// Represents an ordering for a NQ, specifying field and direction
    /// </summary>
    public class QueryOrder
    {
        public QueryMapping QueryMapping { get; }
        public OrderDirection OrderDirection { get; }

        public QueryOrder(QueryMapping queryMapping, OrderDirection orderDirection = OrderDirection.Ascending)
        {
            QueryMapping = queryMapping;
            OrderDirection = orderDirection;
        }
    }
}