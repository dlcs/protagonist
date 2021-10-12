using System.Collections.Generic;
using DLCS.Model.PathElements;

namespace DLCS.Model.Assets.NamedQueries
{
    public class ResourceMappedAssetQuery
    {
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

        public QueryMapping Manifest { get; set; } = QueryMapping.Unset;
        public QueryMapping Sequence { get; set; } = QueryMapping.Unset;
        public QueryMapping Canvas { get; set; } = QueryMapping.Unset;
        public List<string> ArgumentOrder { get; set; } = new();
        public Dictionary<string, string> Pairs { get; set; } = new();
        public int? Space { get; set; }
        public string? SpaceName { get; set; }
        
        // these will become StringReference1, 2, 3
        public string? String1 { get; set; }
        public string? String2 { get; set; }
        public string? String3 { get; set; }

        // these will become NumberReference1, 2, 3
        public long? Number1 { get; set; }
        public long? Number2 { get; set; }
        public long? Number3 { get; set; }

        public CustomerPathElement CustomerPathElement { get; }
        public int Customer => CustomerPathElement.Id;

        public ResourceMappedAssetQuery(CustomerPathElement customerPathElement)
        {
            CustomerPathElement = customerPathElement;
        }
    }
}