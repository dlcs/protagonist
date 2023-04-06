using System;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.AWS.S3.Models;
using Microsoft.Extensions.Options;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.ReverseProxy;

/// <summary>
/// Class for generating proxy paths to S3 resources.
/// </summary>
public class S3ProxyPathGenerator
{
    private readonly IAmazonS3 s3Client;
    private readonly IOptionsMonitor<OrchestratorSettings> orchestratorOptionsMonitor;
    
    public S3ProxyPathGenerator(
        IAmazonS3 s3Client,
        IOptionsMonitor<OrchestratorSettings> orchestratorOptionsMonitor)
    {
        this.s3Client = s3Client;
        this.orchestratorOptionsMonitor = orchestratorOptionsMonitor;
    }
    
    /// <summary>
    /// Get a proxy-able path to resources represented by <see cref="ObjectInBucket"/>.
    /// If UsePresignedUrlsForProxy=true then a PresignedUrl will be generated.
    /// Else this will return a https url direct to  
    /// </summary>
    /// <param name="proxyTarget"><see cref="ObjectInBucket"/> representing proxy target</param>
    /// <param name="isDlcsStorage">true if object is in a DLCS bucket, else false</param>
    /// <returns>String representing URI to redirect to</returns>
    public string GetProxyPath(ObjectInBucket proxyTarget, bool isDlcsStorage)
    {
        var proxySettings = orchestratorOptionsMonitor.CurrentValue.Proxy;
        bool usePresigned = isDlcsStorage
            ? proxySettings.UsePresignedUrlsForDlcs
            : proxySettings.UsePresignedUrlsForOptimised;
        
        if (usePresigned)
        {
            var presignedUrl = s3Client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                Expires = DateTime.UtcNow.AddSeconds(proxySettings.PresignedUrlExpirySecs),
                BucketName = proxyTarget.Bucket,
                Key = proxyTarget.Key,
                Verb = HttpVerb.GET,
                Protocol = Protocol.HTTPS,
            });
            return presignedUrl;
        }

        return proxyTarget.GetHttpUri().ToString();
    }
}