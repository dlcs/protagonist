using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Hydra.Collections;

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

    public static void AddPaging<T>(HydraCollection<T> collection, PartialCollectionViewPagingValues values)
    {
        if (collection.Members == null) return;
        if (collection.TotalItems <= 0) return;
        if (collection.TotalItems > collection.Members.Length)
        {
            int totalPages = collection.TotalItems / values.PageSize;
            if (collection.TotalItems % values.PageSize > 0) totalPages++;
            var baseUrl = collection.Id!.Split('?')[0];
            var partialView = new PartialCollectionView
            {
                Id = $"{baseUrl}?page={values.Page}",
                Page = values.Page,
                PageSize = values.PageSize,
                TotalPages = totalPages
            };
            if (values.Page > 1)
            {
                partialView.First = $"{baseUrl}?page={1}";
                partialView.Previous = $"{baseUrl}?page={values.Page-1}";
            }
            if (values.Page < totalPages)
            {
                partialView.Last = $"{baseUrl}?page={totalPages}";
                partialView.Next = $"{baseUrl}?page={values.Page+1}";
            }

            partialView.Id = AppendCommonParams(partialView.Id, values);
            partialView.First = AppendCommonParams(partialView.First, values);
            partialView.Previous = AppendCommonParams(partialView.Previous, values);
            partialView.Last = AppendCommonParams(partialView.Last, values);
            partialView.Next = AppendCommonParams(partialView.Next, values);

            collection.View = partialView;
        }
    }

    private static string? AppendCommonParams(string? partial, PartialCollectionViewPagingValues values)
    {
        if (partial == null) return null;
        var full = $"{partial}&pageSize={values.PageSize}";
        if (!string.IsNullOrWhiteSpace(values.OrderBy))
        {
            if (values.Descending)
            {
                full = $"{full}&orderByDescending={values.OrderBy}";
            }
            else
            {
                full = $"{full}&orderBy={values.OrderBy}";
            }
        }

        if (values.FurtherParameters != null)
        {
            foreach (var keyValuePair in values.FurtherParameters)
            {
                full = $"{full}&{keyValuePair.Key}={keyValuePair.Value}";
            }
        }

        return full;
    }
}

public class PartialCollectionViewPagingValues
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string? OrderBy { get; set; }
    public bool Descending { get; set; } = false;
    public List<KeyValuePair<string, string>>? FurtherParameters { get; set; }
}