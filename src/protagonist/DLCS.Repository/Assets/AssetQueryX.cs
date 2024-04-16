using System;
using System.Linq;
using System.Linq.Expressions;
using DLCS.Core.Strings;
using DLCS.Model.Assets;
using DLCS.Model.Page;
using Microsoft.EntityFrameworkCore;

namespace DLCS.Repository.Assets;

/// <summary>
/// Extension methods for asset queries.
/// </summary>
public static class AssetQueryX
{
    /// <summary>
    /// Convert provided orderable to an .OrderBy or .OrderByDescending clause.
    /// The orderBy field can be the API version of property or the full property version.
    /// Defaults to "Created" field ordering if no field specified.
    /// </summary>
    public static IQueryable<Asset> AsOrderedAssetQuery(this IQueryable<Asset> assetQuery, IOrderableRequest orderable)
        => assetQuery.AsOrderedAssetQuery(orderable.Field, orderable.Descending);

    /// <summary>
    /// Convert provided orderBy and descending fields to an .OrderBy or .OrderByDescending clause.
    /// The orderBy field can be the API version of property or the full property version.
    /// Defaults to "Created" field ordering if no field specified.
    /// </summary>
    public static IQueryable<Asset> AsOrderedAssetQuery(this IQueryable<Asset> assetQuery, string? orderBy,
        bool descending = false)
    {
        var field = GetPropertyName(orderBy);
        var lambda = (dynamic)CreateExpression(typeof(Asset), field);
        return descending
            ? Queryable.OrderByDescending(assetQuery, lambda)
            : Queryable.OrderBy(assetQuery, lambda);
    }

    private static string GetPropertyName(string? orderBy)
    {
        // This needs to be moved because it knows about hydra name values.
        if (string.IsNullOrWhiteSpace(orderBy) || orderBy.Length < 2)
        {
            return "Created";
        }

        string pascalCase = char.ToUpperInvariant(orderBy[0]) + orderBy.Substring(1);
        return pascalCase switch
        {
            "Number1" => "NumberReference1",
            "Number2" => "NumberReference2",
            "Number3" => "NumberReference3",
            "String1" => "Reference1",
            "String2" => "Reference2",
            "String3" => "Reference3",
            _ => pascalCase
        };
    }


    // Create an Expression from the PropertyName. 
    // I think Split(".") handles nested properties maybe - seems unnecessary but from an SO post
    // "x" means nothing when creating the Parameter, it's just used for debug messages
    private static LambdaExpression CreateExpression(Type type, string propertyName)
    {
        var param = Expression.Parameter(type, "x");

        Expression body = param;
        foreach (var member in propertyName.Split('.'))
        {
            body = Expression.PropertyOrField(body, member);
        }

        return Expression.Lambda(body, param);
    }

    public static IQueryable<Asset> ApplyAssetFilter(this IQueryable<Asset> queryable, 
        AssetFilter? assetFilter, bool filterOnSpace = false)
    {
        if (assetFilter == null)
        {
            return queryable;
        }

        var filtered = queryable;
        if (assetFilter.Reference1.HasText())
        {
            filtered = filtered.Where(a => a.Reference1 == assetFilter.Reference1);
        }
        if (assetFilter.Reference2.HasText())
        {
            filtered = filtered.Where(a => a.Reference2 == assetFilter.Reference2);
        }
        if (assetFilter.Reference3.HasText())
        {
            filtered = filtered.Where(a => a.Reference3 == assetFilter.Reference3);
        }
        if (assetFilter.NumberReference1 != null)
        {
            filtered = filtered.Where(a => a.NumberReference1 == assetFilter.NumberReference1);
        }
        if (assetFilter.NumberReference2 != null)
        {
            filtered = filtered.Where(a => a.NumberReference2 == assetFilter.NumberReference2);
        }
        if (assetFilter.NumberReference3 != null)
        {
            filtered = filtered.Where(a => a.NumberReference3 == assetFilter.NumberReference3);
        }

        if (filterOnSpace && assetFilter.Space is > 0)
        {
            filtered = filtered.Where(a => a.Space == assetFilter.Space.Value);
        }

        return filtered;
    }
    
    /// <summary>
    /// Include asset delivery channels and their associated policies.
    /// </summary>
    public static IQueryable<Asset> IncludeDeliveryChannelsWithPolicy(this IQueryable<Asset> assetQuery)
        => assetQuery.Include(a => a.ImageDeliveryChannels)
            .ThenInclude(dc => dc.DeliveryChannelPolicy);
}