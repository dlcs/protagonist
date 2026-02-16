using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Types;
using DLCS.Model.Customers;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Strategy;

/// <summary>
/// OriginStrategy implementation for 's3-ambient' assets.
/// </summary>
public class S3AmbientOriginStrategy : IOriginStrategy
{
    private readonly IBucketReader bucketReader;
    private readonly ILogger<S3AmbientOriginStrategy> logger;

    public S3AmbientOriginStrategy(IBucketReader bucketReader, ILogger<S3AmbientOriginStrategy> logger)
    {
        this.bucketReader = bucketReader;
        this.logger = logger;
    }

    public async Task<OriginResponse> LoadAssetFromOrigin(string itemDesc, string origin,
        CustomerOriginStrategy? customerOriginStrategy, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Fetching {ItemDesc} from Origin: {Origin}", itemDesc, origin);

        try
        {
            var regionalisedBucket = RegionalisedObjectInBucket.Parse(origin);
            var response = await bucketReader.GetObjectFromBucket(regionalisedBucket, cancellationToken);
            var originResponse = CreateOriginResponse(response);
            return originResponse;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching {ItemDesc} from Origin: {Origin}", itemDesc, origin);
            return OriginResponse.Empty;
        }
    }

    private static OriginResponse CreateOriginResponse(ObjectFromBucket response)
        => new OriginResponse(response.Stream ?? Stream.Null)
            .WithContentLength(response.Headers?.ContentLength)
            .WithContentType(response.Headers?.ContentType);
}
