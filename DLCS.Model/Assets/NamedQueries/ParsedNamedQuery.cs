using DLCS.Core.Guard;
using DLCS.Model.PathElements;

namespace DLCS.Model.Assets.NamedQueries
{
    /// <summary>
    /// This class represents the results of parsing NQ template with specified arguments, specified in the template
    /// and via URL when making request
    /// </summary>
    public class ParsedNamedQuery
    {
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
            Number3
        }

        /// <summary>
        /// Which Asset property to use for specifying Manifest 
        /// </summary>
        /// <remarks>This is currently not used, needs to be implemented</remarks>
        public QueryMapping Manifest { get; set; } = QueryMapping.Unset;
        
        /// <summary>
        /// Which Asset property to use for specifying Sequence 
        /// </summary>
        /// <remarks>This is currently not used, needs to be implemented</remarks>
        public QueryMapping Sequence { get; set; } = QueryMapping.Unset;
        
        /// <summary>
        /// Which Asset property to use for specifying Canvas sequence 
        /// </summary>
        public QueryMapping Canvas { get; set; } = QueryMapping.Unset;
        
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
        /// CustomerPathElement object sent with request
        /// </summary>
        public CustomerPathElement CustomerPathElement { get; }
        
        /// <summary>
        /// CustomerId associated with request
        /// </summary>
        public int Customer => CustomerPathElement.Id;
        
        /// <summary>
        /// Whether the NQ could be parsed correctly
        /// </summary>
        public bool IsFaulty { get; private set; }
        
        /// <summary>
        /// Any error message associated with this NQ, will have value if <see cref="IsFaulty"/> is true.
        /// </summary>
        public string? ErrorMessage { get; private set; }

        public ParsedNamedQuery(CustomerPathElement customerPathElement)
        {
            CustomerPathElement = customerPathElement;
        }

        /// <summary>
        /// Mark this result as containing errors
        /// </summary>
        public void SetError(string errorMessage)
        {
            ErrorMessage = errorMessage.ThrowIfNullOrWhiteSpace(nameof(errorMessage));
            IsFaulty = true;
        }
    }
}