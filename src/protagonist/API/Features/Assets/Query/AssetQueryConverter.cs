using DLCS.Core.Strings;
using DLCS.HydraModel;
using DLCS.Web.Requests;
using Microsoft.AspNetCore.Http;

namespace API.Features.Assets.Query;

/// <summary>
/// Helpers for dealing with <see cref="AssetQueryModel"/> and related classes
/// </summary>
public static class AssetQueryConverter
{
    public static ImageQuery ToImageQuery(this AssetFilter assetFilter) =>
        new()
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
    
    /// <summary>
    /// Attempt to parse an <see cref="AssetQueryModel"/> from incoming request.
    /// </summary>
    public static AssetQueryModel GetAssetQuery(this HttpRequest request)
    {
        var filterModel = request.GetAssetFilter();
        var includeModel = request.GetAssetInclude();
        return new AssetQueryModel(filterModel, includeModel);
    }

    private static AssetInclude? GetAssetInclude(this HttpRequest request)
    {
        var include = request.GetFirstQueryParamValue(QueryParameters.Include);
        return include.HasText()
            ? new AssetInclude(include.Split(',', StringSplitOptions.RemoveEmptyEntries))
            : null;
    }

    private static AssetFilter? GetAssetFilter(this HttpRequest request)
    {
        // First get model from the "q" parameter
        var modelFromQParam = request.GetAssetFilterFromQParam();
        
        // Which can be overridden by specific query string params
        return request.UpdateAssetFilterFromQueryStringParams(modelFromQParam);
    }
    
    private static AssetFilter? GetAssetFilterFromQParam(this HttpRequest request)
    {
        var q = request.GetFirstQueryParamValue(QueryParameters.Query);
        if (!q.HasText()) return null;

        var imageQuery = ImageQuery.Parse(q);
        return imageQuery?.ToAssetFilter();
    }

    /// <summary>
    /// Inspect the request for string1, number1 etc metadata fields.
    /// Create a new <see cref="AssetFilter" /> if present, or add to the one passed in.
    /// </summary>
    /// <returns>An <see cref="AssetFilter" />, or null if none passed in and no query string params present.</returns>
    private static AssetFilter? UpdateAssetFilterFromQueryStringParams(this HttpRequest request,
        AssetFilter? assetFilter)
    {
        var string1 = request.GetFirstQueryParamValue("string1");
        if (string1.HasText())
        {
            SafeFilter().Reference1 = string1;
        }

        var string2 = request.GetFirstQueryParamValue("string2");
        if (string2.HasText())
        {
            SafeFilter().Reference2 = string2;
        }

        var string3 = request.GetFirstQueryParamValue("string3");
        if (string3.HasText())
        {
            SafeFilter().Reference3 = string3;
        }

        var number1 = request.GetFirstQueryParamValueAsInt("number1");
        if (number1 != null)
        {
            SafeFilter().NumberReference1 = number1;
        }

        var number2 = request.GetFirstQueryParamValueAsInt("number2");
        if (number2 != null)
        {
            SafeFilter().NumberReference2 = number2;
        }

        var number3 = request.GetFirstQueryParamValueAsInt("number3");
        if (number3 != null)
        {
            SafeFilter().NumberReference3 = number3;
        }

        var manifests = request.GetFirstQueryParamValueAsArray("manifests");
        if (manifests != null)
        {
            SafeFilter().Manifests = manifests;
        }

        return assetFilter;

        AssetFilter SafeFilter() => assetFilter ??= new AssetFilter();
    }

    /// <summary>
    /// Convert the Hydra <see cref="ImageQuery"/> to a <see cref="AssetFilter"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="ImageQuery"/> is a Hydra object, map to similar object for business logic use
    /// </remarks>
    private static AssetFilter ToAssetFilter(this ImageQuery imageQuery) =>
        new()
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
