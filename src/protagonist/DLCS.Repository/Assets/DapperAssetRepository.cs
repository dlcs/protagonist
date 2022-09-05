using System;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using LazyCache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Assets;

/// <summary>
/// Implementation of <see cref="IAssetRepository"/> using Dapper for data access.
/// </summary>
public class DapperAssetRepository : AssetRepositoryCachingBase, IDapperConfigRepository
{
    public IConfiguration Configuration { get; }
    
    public DapperAssetRepository(
        IConfiguration configuration, 
        IAppCache appCache,
        IOptions<CacheSettings> cacheOptions,
        ILogger<DapperAssetRepository> logger) : base(appCache, cacheOptions, logger)
    {
        Configuration = configuration;
    }
    
    public override async Task<ImageLocation?> GetImageLocation(AssetId assetId) 
        => await this.QuerySingleOrDefaultAsync<ImageLocation>(ImageLocationSql, new {Id = assetId.ToString()});
    
    protected override Task<ResultStatus<DeleteResult>> DeleteAssetFromDatabase(string id)
    {
        throw new NotImplementedException();
    }

    protected override async Task<Asset?> GetAssetFromDatabase(string id)
    {
        dynamic? rawAsset = await this.QuerySingleOrDefaultAsync(AssetSql, new { Id = id });
        if (rawAsset == null)
        {
            return null;
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
            ImageOptimisationPolicy = rawAsset.ImageOptimisationPolicy,
            NotForDelivery = rawAsset.NotForDelivery
        };
    }

    private const string AssetSql = @"
SELECT ""Id"", ""Customer"", ""Space"", ""Created"", ""Origin"", ""Tags"", ""Roles"", 
""PreservedUri"", ""Reference1"", ""Reference2"", ""Reference3"", ""MaxUnauthorised"", 
""NumberReference1"", ""NumberReference2"", ""NumberReference3"", ""Width"", 
""Height"", ""Error"", ""Batch"", ""Finished"", ""Ingesting"", ""ImageOptimisationPolicy"", 
""ThumbnailPolicy"", ""Family"", ""MediaType"", ""Duration"", ""NotForDelivery""
  FROM public.""Images""
  WHERE ""Id""=@Id;";

    private const string ImageLocationSql =
        "SELECT \"Id\", \"S3\", \"Nas\" FROM public.\"ImageLocation\" WHERE \"Id\"=@Id;";
}