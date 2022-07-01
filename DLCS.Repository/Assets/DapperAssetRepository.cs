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
        
        public Task<Asset?> GetAsset(string id)
        {
            return GetAsset(id, false);
        }

        public Task<Asset?> GetAsset(AssetId id)
        {
            return GetAsset(id, false);
        }
        
        public async Task<Asset?> GetAsset(string id, bool noCache)
        {
            var asset = await GetAssetInternal(id, noCache);
            return asset.Id == NullAsset.Id ? null : asset;
        }

        public Task<Asset?> GetAsset(AssetId id, bool noCache)
            => GetAsset(id.ToString(), noCache);

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
        /// <param name="putAsset">
        /// An Asset that is ready to be inserted/updated in the DB, that
        /// has usually come from an incoming
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <param name="operation">TEMPORARY</param>
        public async Task Put(Asset putAsset, CancellationToken cancellationToken, string operation)
        {
            // Consider that this may be used for an already-tracked entity, or more likely, one that's
            // been constructed from API calls and therefore not tracked.
            if (dlcsContext.Images.Local.Any(asset => asset.Id == putAsset.Id))
            {
                // asset with this ID is already being tracked
                if (dlcsContext.Entry(putAsset).State == EntityState.Detached)
                {
                    // but it ain't this instance!
                    // what do we do? EF will throw an exception if we try to save this. 
                    throw new InvalidOperationException("There is already an Asset with this ID being tracked");
                }
                // As it's already tracked, we don't need to do anything here.
            }
            else
            {
                var existing = await dlcsContext.Images.FindAsync(new object[] { putAsset.Id }, cancellationToken);
                if (existing == null)
                {
                    await dlcsContext.Images.AddAsync(putAsset, cancellationToken);
                }
                else
                {
                    dlcsContext.Images.Update(putAsset);
                }
            }
        
            
            
            
            
            
            // TODO: what does this have in common with Patch?
            // Business logic has already happened in the Mediatr handler.
            // Whatever you want to put in the database...
            
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

        private async Task<Asset> GetAssetInternal(string id, bool noCache = false)
        {
            var key = $"asset:{id}";
            if (noCache)
            {
                appCache.Remove(key);
            }
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
