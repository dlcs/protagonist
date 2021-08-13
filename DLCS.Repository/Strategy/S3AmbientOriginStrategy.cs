using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Customer;
using DLCS.Model.Storage;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Strategy
{
    /// <summary>
    /// OriginStrategy implementation for 's3-ambient' assets.
    /// </summary>
    public class S3AmbientOriginStrategy : SafetyCheckOriginStrategy
    {
        private readonly IBucketReader bucketReader;
        private readonly ILogger<S3AmbientOriginStrategy> logger;

        public S3AmbientOriginStrategy(IBucketReader bucketReader, ILogger<S3AmbientOriginStrategy> logger)
        {
            this.bucketReader = bucketReader;
            this.logger = logger;
        }
        
        protected override async Task<OriginResponse?> LoadAssetFromOriginImpl(AssetId assetId, string origin,
            CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default)
        {
            logger.LogDebug("Fetching {asset} from Origin: {url}", assetId, origin);

            try
            {
                var regionalisedBucket = RegionalisedObjectInBucket.Parse(origin);
                var response = await bucketReader.GetObjectFromBucket(regionalisedBucket);
                var originResponse = CreateOriginResponse(response);
                return originResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching {asset} from Origin: {url}", assetId, origin);
                return null;
            }
        }

        private static OriginResponse CreateOriginResponse(ObjectFromBucket response)
            => new OriginResponse(response.Stream)
                .WithContentLength(response.Headers.ContentLength)
                .WithContentType(response.Headers.ContentType);
    }
}