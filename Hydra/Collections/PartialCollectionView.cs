using Newtonsoft.Json;

namespace Hydra.Collections
{
    public class PartialCollectionView : JsonLdBaseWithHydraContext
    {
        public override string Type => "PartialCollectionView";

        [JsonProperty(Order = 11, PropertyName = "first")]
        public string? First { get; set; }

        [JsonProperty(Order = 12, PropertyName = "previous")]
        public string? Previous { get; set; }

        [JsonProperty(Order = 13, PropertyName = "next")]
        public string? Next { get; set; }

        [JsonProperty(Order = 14, PropertyName = "last")]
        public string? Last { get; set; }

        // These three properties are not part of the Hydra specification, but they are very handy.        
        [JsonProperty(Order = 21, PropertyName = "page")]
        public int Page { get; set; }
        
        [JsonProperty(Order = 22, PropertyName = "pageSize")]
        public int PageSize { get; set; }        
        
        [JsonProperty(Order = 23, PropertyName = "totalPages")]
        public int TotalPages { get; set; }

        public static void AddPaging<T>(HydraCollection<T> collection, 
            int page, int pageSize, string? orderBy = null, bool ascending = true)
        {
            if (collection.Members == null) return;
            if (collection.TotalItems <= 0) return;
            if (collection.TotalItems > collection.Members.Length)
            {
                int totalPages = collection.TotalItems / pageSize;
                if (collection.TotalItems % pageSize > 0) totalPages++;
                var baseUrl = collection.Id!.Split('?')[0];
                var partialView = new PartialCollectionView
                {
                    Id = $"{baseUrl}?page={page}&pageSize={pageSize}",
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };
                if (page > 1)
                {
                    partialView.First = $"{baseUrl}?page={1}&pageSize={pageSize}";
                    partialView.Previous = $"{baseUrl}?page={page-1}&pageSize={pageSize}";
                }
                if (page < totalPages)
                {
                    partialView.Last = $"{baseUrl}?page={totalPages}&pageSize={pageSize}";
                    partialView.Next = $"{baseUrl}?page={page+1}&pageSize={pageSize}";
                }

                partialView.Id = AppendCommonParams(partialView.Id, pageSize, orderBy, ascending);
                partialView.First = AppendCommonParams(partialView.First, pageSize, orderBy, ascending);
                partialView.Previous = AppendCommonParams(partialView.Previous, pageSize, orderBy, ascending);
                partialView.Last = AppendCommonParams(partialView.Last, pageSize, orderBy, ascending);
                partialView.Next = AppendCommonParams(partialView.Next, pageSize, orderBy, ascending);

                collection.View = partialView;
            }
        }

        private static string? AppendCommonParams(string? partial, int pageSize, string? orderBy, bool ascending)
        {
            if (partial == null) return null;
            var full = $"{partial}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(orderBy))
            {
                full = $"{full}&orderBy={orderBy}";
                if (ascending == false)
                {
                    full = $"{full}&ascending=false";
                }
            }

            return full;
        }
    }
}