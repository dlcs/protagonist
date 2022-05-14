using System;
using System.Linq;
using System.Linq.Expressions;
using DLCS.Model.Assets;

namespace DLCS.Repository.Assets
{
    /// <summary>
    /// Extension methods for asset queries.
    /// </summary>
    public static class AssetQueryX
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="assetQuery"></param>
        /// <param name="orderBy"></param>
        /// <param name="ascending"></param>
        /// <returns></returns>
        public static IQueryable<Asset> AsOrderedAssetQuery(this IQueryable<Asset> assetQuery, string orderBy, bool ascending = true)
        {
            var field = GetPropertyName(orderBy);
            var lambda = (dynamic)CreateExpression(typeof(Asset), field);
            return ascending
                ? Queryable.OrderBy(assetQuery, lambda)
                : Queryable.OrderByDescending(assetQuery, lambda);
        }

        private static string GetPropertyName(string orderBy)
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
    }
}