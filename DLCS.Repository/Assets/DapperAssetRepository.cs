using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository.Caching;
using LazyCache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace DLCS.Repository.Assets
{
    /// <summary>
    /// Implementation of <see cref="IAssetRepository"/> using Dapper for data access.
    /// </summary>
    public class DapperAssetRepository : DapperRepository, IAssetRepository
    {
        // This repository uses both Dapper and EF...
        private readonly DlcsContext dlcsContext;
        private readonly CacheSettings cacheSettings;
        private readonly IAppCache appCache;
        private readonly ILogger<DapperAssetRepository> logger;
        private static readonly Asset NullAsset = new() { Id = "__nullasset__" };

        public DapperAssetRepository(
            DlcsContext dlcsContext,
            IConfiguration configuration, 
            IAppCache appCache,
            IOptions<CacheSettings> cacheOptions,
            ILogger<DapperAssetRepository> logger) : base(configuration)
        {
            this.dlcsContext = dlcsContext;
            this.appCache = appCache;
            this.logger = logger;
            cacheSettings = cacheOptions.Value;
        }

        public async Task<Asset?> GetAsset(string id)
        {
            var asset = await GetAssetInternal(id);
            return asset.Id == NullAsset.Id ? null : asset;
        }

        public Task<Asset?> GetAsset(AssetId id)
            => GetAsset(id.ToString());

        public async Task<ImageLocation> GetImageLocation(AssetId assetId)
        {
            // There's an EF version of this in the other repo
            return await QuerySingleOrDefaultAsync<ImageLocation>(ImageLocationSql, new {Id = assetId.ToString()});
        }


        private void RemoveImageLocationInternal(string assetId)
        {
            var entity = new ImageLocation { Id = assetId };
            var entry = dlcsContext.Entry(entity);
            dlcsContext.Remove(entry);
        }

        public async Task<PageOfAssets?> GetPageOfAssets(int customerId, int spaceId, int page, int pageSize, 
            string orderBy, bool ascending, CancellationToken cancellationToken)
        {
            var space = await dlcsContext.Spaces.SingleOrDefaultAsync(
                s => s.Customer == customerId && s.Id == spaceId, cancellationToken: cancellationToken);
            if (space == null)
            {
                return null;
            }
            var result = new PageOfAssets
            {
                Page = page,
                Total = await dlcsContext.Images.CountAsync(
                    a => a.Customer == customerId && a.Space == spaceId, cancellationToken: cancellationToken),
                Assets = await dlcsContext.Images.AsNoTracking()
                    .Where(a => a.Customer == customerId && a.Space == spaceId)
                    .AsOrderedAssetQuery(orderBy, ascending)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken: cancellationToken)
            };
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="putAsset"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="operation">TEMPORARY</param>
        public async Task Put(Asset putAsset, CancellationToken cancellationToken, string operation)
        {
            // putAsset has not been obtained from a DB context.
            // TODO: what does this have in common with Patch?
            // Business logic has already happened in the Mediatr handler.
            // Whatever you want to put in the database...
            var dbAsset = await dlcsContext.Images.FindAsync(new object[] { putAsset.Id }, cancellationToken);
            if (dbAsset == null)
            {
                dbAsset = new Asset { Id = putAsset.Id };
                dlcsContext.Images.Add(dbAsset);
            }
            CopyAssetFields(putAsset, dbAsset);
            
            // In deliverator, a PATCH of an asset deletes the image location
            // but a PUT creates a new, blank one.
            // Ideally this is a single line,
            // RemoveImageLocationInternal(putAsset.Id);
            // This would be fronted by a cache.
            if (operation == "PATCH")
            {
                // https://github.com/digirati-co-uk/deliverator/blob/87f6cfde97be94d2e9e00c11c4dc0fcfacfdd087/API/Architecture/Request/API/Entities/CustomerSpaceImage.cs#L554
                RemoveImageLocationInternal(putAsset.Id);
            }
            else
            {
                // CreateSkeletonImageLocationBehaviour
                // UpdateImageLocationBehaviour
                // https://github.com/digirati-co-uk/deliverator/blob/87f6cfde97be94d2e9e00c11c4dc0fcfacfdd087/API/Architecture/Request/API/Entities/CustomerSpaceImage.cs#L303
                var imageLocation = await dlcsContext.ImageLocations.FindAsync(new object[] { putAsset.Id }, cancellationToken);
                if (imageLocation == null)
                {
                    imageLocation = new ImageLocation { Id = putAsset.Id };
                    dlcsContext.ImageLocations.Add(imageLocation);
                }
                imageLocation.S3 = string.Empty;
                imageLocation.Nas = string.Empty;
            }
            
            await dlcsContext.SaveChangesAsync(cancellationToken);
        }

        private static void CopyAssetFields(Asset source, Asset dest)
        {
            // https://github.com/digirati-co-uk/deliverator/blob/8e7eb3ea7a839f4caad7b9085b7680a69ae726ca/DLCS.SqlServer/Data/Store/SqlImageStore.cs#L187
            dest.Customer = source.Customer;
            dest.Created = source.Created;
            dest.Origin = source.Origin;
            dest.PreservedUri = source.PreservedUri;
            dest.Space = source.Space;
            dest.Tags = source.Tags;
            dest.Roles = source.Roles;
            dest.Reference1 = source.Reference1;
            dest.Reference2 = source.Reference2;
            dest.Reference3 = source.Reference3;
            dest.MaxUnauthorised = source.MaxUnauthorised;
            dest.NumberReference1 = source.NumberReference1;
            dest.NumberReference2 = source.NumberReference2;
            dest.NumberReference3 = source.NumberReference3;
            dest.Width = source.Width;
            dest.Height = source.Height;
            dest.Duration = source.Duration;
            dest.Error = source.Error;
            dest.Batch = source.Batch;
            if (source.Finished != DateTime.MinValue)
            {
                dest.Finished = source.Finished;
            }
            dest.Ingesting = source.Ingesting;
            dest.ImageOptimisationPolicy = source.ImageOptimisationPolicy;
            dest.ThumbnailPolicy = source.ThumbnailPolicy;
            dest.Family = source.Family;
            dest.MediaType = source.MediaType;
        }

        private async Task<Asset> GetAssetInternal(string id)
        {
            var key = $"asset:{id}";
            return await appCache.GetOrAddAsync(key, async entry =>
            {
                logger.LogInformation("Refreshing assetCache from database {Asset}", id);
                dynamic? rawAsset = await QuerySingleOrDefaultAsync(AssetSql, new { Id = id });
                if (rawAsset == null)
                {
                    entry.AbsoluteExpirationRelativeToNow =
                        TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Short));
                    return NullAsset;
                }

                return new Asset
                {
                    Batch = rawAsset.Batch,
                    Created = rawAsset.Created,
                    Customer = rawAsset.Customer,
                    Duration = rawAsset.Duration,
                    Error = rawAsset.Error,
                    Family = (AssetFamily)rawAsset.Family.ToString()[0],
                    Finished = rawAsset.Finished,
                    Height = rawAsset.Height,
                    Id = rawAsset.Id,
                    Ingesting = rawAsset.Ingesting,
                    Origin = rawAsset.Origin,
                    Reference1 = rawAsset.Reference1,
                    Reference2 = rawAsset.Reference2,
                    Reference3 = rawAsset.Reference3,
                    Roles = rawAsset.Roles,
                    Space = rawAsset.Space,
                    Tags = rawAsset.Tags,
                    Width = rawAsset.Width,
                    MaxUnauthorised = rawAsset.MaxUnauthorised,
                    MediaType = rawAsset.MediaType,
                    NumberReference1 = rawAsset.NumberReference1,
                    NumberReference2 = rawAsset.NumberReference2,
                    NumberReference3 = rawAsset.NumberReference3,
                    PreservedUri = rawAsset.PreservedUri,
                    ThumbnailPolicy = rawAsset.ThumbnailPolicy,
                    ImageOptimisationPolicy = rawAsset.ImageOptimisationPolicy
                };
            }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Short));
        }
        
        private const string AssetSql = @"
SELECT ""Id"", ""Customer"", ""Space"", ""Created"", ""Origin"", ""Tags"", ""Roles"", 
""PreservedUri"", ""Reference1"", ""Reference2"", ""Reference3"", ""MaxUnauthorised"", 
""NumberReference1"", ""NumberReference2"", ""NumberReference3"", ""Width"", 
""Height"", ""Error"", ""Batch"", ""Finished"", ""Ingesting"", ""ImageOptimisationPolicy"", 
""ThumbnailPolicy"", ""Family"", ""MediaType"", ""Duration""
  FROM public.""Images""
  WHERE ""Id""=@Id;";

        private const string ImageLocationSql =
            "SELECT \"Id\", \"S3\", \"Nas\" FROM public.\"ImageLocation\" WHERE \"Id\"=@Id;";
    }
}
