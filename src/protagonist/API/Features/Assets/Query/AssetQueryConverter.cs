using DLCS.Core.Strings;
using DLCS.HydraModel;
using DLCS.Web.Requests;
using Microsoft.AspNetCore.Http;

namespace API.Features.Assets.Query;

public static class AssetQueryConverter
{
    /// <summary>
    /// We don't want to use the Hydra ImageQuery class inside the DLCS business logic, it's an HTTP layer JSON construct.
    /// So we convert to a very similar object.
    /// Other code might reference the Hydra class to build clients but won't reference this.
    /// </summary>
    private static AssetFilter ToAssetFilter(this ImageQuery imageQuery)
    {
        return new AssetFilter
        {
            Space = imageQuery.Space,
            Reference1 = imageQuery.String1,
            Reference2 = imageQuery.String2,
            Reference3 = imageQuery.String3,
            NumberReference1 = imageQuery.Number1,
            NumberReference2 = imageQuery.Number2,
            NumberReference3 = imageQuery.Number3,
            Manifests = imageQuery.Manifests
        };
    }

    public static ImageQuery ToImageQuery(this AssetFilter assetFilter)
    {
        return new ImageQuery
        {
            Space = assetFilter.Space,
            String1 = assetFilter.Reference1,
            String2 = assetFilter.Reference2,
            String3 = assetFilter.Reference3,
            Number1 = assetFilter.NumberReference1,
            Number2 = assetFilter.NumberReference2,
            Number3 = assetFilter.NumberReference3,
            Manifests = assetFilter.Manifests
        };
    }

    /// <summary>
    /// Attempt to parse an AssetFilter from a supplied ImageQuery object on the query string.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="q">Supply a q; if not present will attempt to parse from request</param>
    /// <returns></returns>
    public static AssetFilter? GetAssetFilterFromQParam(this HttpRequest request, string? q = null)
    {
        q ??= request.GetFirstQueryParamValue("q");
        if (q.HasText())
        {
            var imageQuery = ImageQuery.Parse(q);
            if (imageQuery != null)
            {
                return imageQuery.ToAssetFilter();
            }
        }

        return null;
    }

    // TODO - move all the AssetFilter stuff to 1 single class, it's not AssetConverting
    public static AssetQueryModel GetAssetQuery(this HttpRequest request)
    {
        // TODO: Tidy up calls to this method - make it private
        var filterFromModel = request.GetAssetFilterFromQParam();
        
        // TODO: Make this private
        var fullModel = request.UpdateAssetFilterFromQueryStringParams(filterFromModel);
        
        // TODO: Maybe extract this to a separate method?
        // TODO: Move 'include' and 'q' to a common place, shared with HydraController
        var include = request.GetFirstQueryParamValue("include");
        AssetInclude? assetInclude = null;
        if (include.HasText())
        {
            assetInclude = new AssetInclude
            {
                Include = include.Split(',', StringSplitOptions.RemoveEmptyEntries)
            };
        }

        // TODO - is this right? Always return a new object, callers can handle internal being null or no
        // return fullModel == null && assetInclude == null ? null : new AssetQueryModel(fullModel, assetInclude);
        return new AssetQueryModel(fullModel, assetInclude);
    }

    /// <summary>
    /// Inspect the request for string1, number1 etc metadata fields.
    /// Create a new AssetFilter if present, or add to the one passed in.
    /// </summary>
    /// <returns>An AssetFilter, or null if none passed in and no query string params present.</returns>
    public static AssetFilter? UpdateAssetFilterFromQueryStringParams(this HttpRequest request, AssetFilter? assetFilter)
    {
        // TODO: Tidy up these calls?
        var string1 = request.GetFirstQueryParamValue("string1");
        if (string1.HasText())
        {
            assetFilter ??= new AssetFilter();
            assetFilter.Reference1 = string1;
        }
        var string2 = request.GetFirstQueryParamValue("string2");
        if (string2.HasText())
        {
            assetFilter ??= new AssetFilter();
            assetFilter.Reference2 = string2;
        }
        var string3 = request.GetFirstQueryParamValue("string3");
        if (string3.HasText())
        {
            assetFilter ??= new AssetFilter();
            assetFilter.Reference3 = string3;
        }

        var number1 = request.GetFirstQueryParamValueAsInt("number1");
        if (number1 != null)
        {
            assetFilter ??= new AssetFilter();
            assetFilter.NumberReference1 = number1;
        }
        var number2 = request.GetFirstQueryParamValueAsInt("number2");
        if (number2 != null)
        {
            assetFilter ??= new AssetFilter();
            assetFilter.NumberReference2 = number2;
        }
        var number3 = request.GetFirstQueryParamValueAsInt("number3");
        if (number3 != null)
        {
            assetFilter ??= new AssetFilter();
            assetFilter.NumberReference3 = number3;
        }
        var manifests = request.GetFirstQueryParamValueAsArray("manifests");
        if (manifests != null)
        {
            assetFilter ??= new AssetFilter();
            assetFilter.Manifests = manifests;
        }
        
        return assetFilter;
    }
}
