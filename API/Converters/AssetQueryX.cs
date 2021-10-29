using System.Linq;
using DLCS.Model.Assets;
using DLCS.HydraModel;

namespace API.Converters
{
    public static class AssetQueryX
    {
        public static IQueryable<Asset> AsOrderedAssetQuery(this IQueryable<Asset> assetQuery, string orderBy)
        {
            if (string.IsNullOrWhiteSpace(orderBy)) return assetQuery.OrderBy(a => a.Id);
            IQueryable<Asset> orderedAssetQuery = orderBy switch
            {
                nameof(Image.Number1) => assetQuery.OrderBy(a => a.NumberReference1),
                nameof(Image.Number2) => assetQuery.OrderBy(a => a.NumberReference2),
                nameof(Image.Number3) => assetQuery.OrderBy(a => a.NumberReference3),
                nameof(Image.String1) => assetQuery.OrderBy(a => a.Reference1),
                nameof(Image.String2) => assetQuery.OrderBy(a => a.Reference2),
                nameof(Image.String3) => assetQuery.OrderBy(a => a.Reference3),
                nameof(Image.CustomerId) => assetQuery.OrderBy(a => a.Customer),
                nameof(Image.Space) => assetQuery.OrderBy(a => a.Space),
                nameof(Image.Created) => assetQuery.OrderBy(a => a.Reference3),
                nameof(Image.Width) => assetQuery.OrderBy(a => a.Width),
                nameof(Image.Height) => assetQuery.OrderBy(a => a.Height),
                nameof(Image.Finished) => assetQuery.OrderBy(a => a.Finished),
                nameof(Image.Duration) => assetQuery.OrderBy(a => a.Duration),
                _ => assetQuery.OrderBy(a => a.Id)
            };
            return orderedAssetQuery;
        }
        
        
        public static IQueryable<Asset> AsOrderedAssetQueryDescending(this IQueryable<Asset> assetQuery, string orderBy)
        {
            if (string.IsNullOrWhiteSpace(orderBy)) return assetQuery.OrderByDescending(a => a.Id);
            IQueryable<Asset> orderedAssetQuery = orderBy switch
            {
                nameof(Image.Number1) => assetQuery.OrderByDescending(a => a.NumberReference1),
                nameof(Image.Number2) => assetQuery.OrderByDescending(a => a.NumberReference2),
                nameof(Image.Number3) => assetQuery.OrderByDescending(a => a.NumberReference3),
                nameof(Image.String1) => assetQuery.OrderByDescending(a => a.Reference1),
                nameof(Image.String2) => assetQuery.OrderByDescending(a => a.Reference2),
                nameof(Image.String3) => assetQuery.OrderByDescending(a => a.Reference3),
                nameof(Image.CustomerId) => assetQuery.OrderByDescending(a => a.Customer),
                nameof(Image.Space) => assetQuery.OrderByDescending(a => a.Space),
                nameof(Image.Created) => assetQuery.OrderByDescending(a => a.Reference3),
                nameof(Image.Width) => assetQuery.OrderByDescending(a => a.Width),
                nameof(Image.Height) => assetQuery.OrderByDescending(a => a.Height),
                nameof(Image.Finished) => assetQuery.OrderByDescending(a => a.Finished),
                nameof(Image.Duration) => assetQuery.OrderByDescending(a => a.Duration),
                _ => assetQuery.OrderByDescending(a => a.Id)
            };
            return orderedAssetQuery;
        }
    }
}