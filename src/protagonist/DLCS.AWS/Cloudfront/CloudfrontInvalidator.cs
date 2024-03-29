﻿using Amazon.CloudFront;
using Amazon.CloudFront.Model;
using DLCS.AWS.Settings;
using DLCS.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.Cloudfront;

public class CloudfrontInvalidator : ICacheInvalidator
{
    private readonly ILogger<CloudfrontInvalidator> logger;
    private readonly IAmazonCloudFront client;
    private CloudfrontSettings cloudfrontSettings;
    
    
    public CloudfrontInvalidator(
        ILogger<CloudfrontInvalidator> logger,
        IAmazonCloudFront client,
        IOptions<AWSSettings> settings)
    {
        this.logger = logger;
        this.client = client;
        cloudfrontSettings = settings.Value.Cloudfront;
    }
    
    public async Task<bool> InvalidateCdnCache(List<string> invalidationPaths, CancellationToken cancellationToken)
    {
        var invalidationRequest = new CreateInvalidationRequest
        {
            DistributionId = cloudfrontSettings.DistributionId,
            InvalidationBatch = new InvalidationBatch
            {
                Paths = new Paths
                {
                    Quantity = invalidationPaths.Count,
                    Items = invalidationPaths
                },
                CallerReference = DateTime.Now.Ticks.ToString()
            }
        };

        try
        {
            logger.LogDebug("invalidating cloud front. {Paths}", invalidationPaths);
            var invalidationResult = await client.CreateInvalidationAsync(invalidationRequest, cancellationToken);

            return invalidationResult.HttpStatusCode.IsSuccess();
        }
        catch (NoSuchDistributionException ex)
        {
            logger.LogError(ex, "No such distribution {Distribution}", 
                cloudfrontSettings.DistributionId);
            throw;
        }
        catch (TooManyInvalidationsInProgressException ex)
        {
            logger.LogError(ex, "Too many invalidations in progress. {Distribution}", 
                cloudfrontSettings.DistributionId);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error invalidating the cache for distribution {Distribution}", 
                cloudfrontSettings.DistributionId);
            return false;
        }
    }
}