using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using API.Exceptions;
using API.Infrastructure.Requests;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Model.Assets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace API.Features.Assets.Query;

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
    private static IQueryable<Asset> AsOrderedAssetQuery(this IQueryable<Asset> assetQuery, string? orderBy,
        bool descending = false)
    {
        var field = GetPropertyName(orderBy);
        var lambda = (dynamic)CreateExpression(typeof(Asset), field);
        return descending
            ? Queryable.OrderByDescending(assetQuery, lambda)
            : Queryable.OrderBy(assetQuery, lambda);
    }

    // Properties that an ORDER BY clause can be built for: scalar columns and primitive-collection
    // columns (e.g. Manifests, DeliveryChannels), but not collections of related entities.
    // Keyed case-insensitively as orderBy field matching has always ignored case.
    private static readonly Dictionary<string, string> OrderableProperties = typeof(Asset)
        .GetProperties()
        .Where(p => IsOrderable(p.PropertyType))
        .ToDictionary(p => p.Name, p => p.Name, StringComparer.OrdinalIgnoreCase);

    private static bool IsOrderable(Type propertyType)
    {
        if (propertyType == typeof(string)) return true;

        var elementType = propertyType.IsArray
            ? propertyType.GetElementType()
            : propertyType.IsGenericType && typeof(IEnumerable).IsAssignableFrom(propertyType)
                ? propertyType.GetGenericArguments()[0]
                : null;
        return elementType == null || elementType == typeof(string) || elementType.IsValueType;
    }

    private static string GetPropertyName(string? orderBy)
    {
        // This needs to be moved because it knows about hydra name values.
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return "Created";
        }

        string pascalCase = char.ToUpperInvariant(orderBy[0]) + orderBy.Substring(1);
        var mapped = pascalCase switch
        {
            "Number1" => "NumberReference1",
            "Number2" => "NumberReference2",
            "Number3" => "NumberReference3",
            "String1" => "Reference1",
            "String2" => "Reference2",
            "String3" => "Reference3",
            _ => pascalCase
        };

        if (!OrderableProperties.TryGetValue(mapped, out var propertyName))
        {
            throw new BadRequestException($"Cannot order by field '{orderBy}'");
        }

        return propertyName;
    }
    
    /// <summary>
    /// Create an Expression from the PropertyName.
    /// </summary>
    /// <remarks>
    /// I think Split(".") handles nested properties maybe - seems unnecessary but from an SO post
    /// "x" means nothing when creating the Parameter, it's just used for debug messages
    /// </remarks>
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

    /// <summary>
    /// Apply asset filter to queryable (this is filtering only - it doesn't handle .Include() calls)
    /// </summary>
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
        if (!assetFilter.Manifests.IsNullOrEmpty())
        {
            filtered = filtered.Where(a =>
                a.Manifests!.Any(manifest => assetFilter.Manifests.Contains(manifest)));
        }

        if (filterOnSpace && assetFilter.Space is > 0)
        {
            filtered = filtered.Where(a => a.Space == assetFilter.Space.Value);
        }

        return filtered;
    }

    /// <summary>
    /// Helper to .Include() related entities in accordance with the <see cref="AssetInclude"/>.
    /// </summary>
    public static IQueryable<Asset> IncludeRelated(this IQueryable<Asset> assetQuery, AssetInclude? assetFilter) =>
        assetFilter?.IncludesField(IncludeFields.Adjuncts) == true
            ? assetQuery.Include(a => a.Adjuncts!)
            : assetQuery;

    /// <summary>
    /// Helper to .ThenInclude() related entities in accordance with the <see cref="AssetInclude"/>.
    /// </summary>
    public static IQueryable<TEntity> IncludeRelated<TEntity>(
        this IIncludableQueryable<TEntity, Asset> assetQuery, AssetInclude? assetFilter) where TEntity : class =>
        assetFilter?.IncludesField(IncludeFields.Adjuncts) == true
            ? assetQuery.ThenInclude(a => a.Adjuncts!)
            : assetQuery;
}

