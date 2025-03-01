﻿using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Streams;
using IIIF;
using IIIF.ImageApi;
using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;
using IIIF.Serialisation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Infrastructure;
using Orchestrator.Settings;

namespace Orchestrator.Features.Images.ImageServer;

/// <summary>
/// Service for managing the fetching and storing of info.json requests.
/// </summary>
public class InfoJsonService
{
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly IBucketReader bucketReader;
    private readonly IBucketWriter bucketWriter;
    private readonly InfoJsonConstructorResolver infoJsonConstructorResolver;
    private readonly OrchestratorSettings orchestratorSettings;
    private readonly ILogger<InfoJsonService> logger;

    public InfoJsonService(
        IStorageKeyGenerator storageKeyGenerator,
        IBucketReader bucketReader,
        IBucketWriter bucketWriter,
        InfoJsonConstructorResolver infoJsonConstructorResolver,
        IOptions<OrchestratorSettings> orchestratorSettings,
        ILogger<InfoJsonService> logger)
    {
        this.storageKeyGenerator = storageKeyGenerator;
        this.bucketReader = bucketReader;
        this.bucketWriter = bucketWriter;
        this.infoJsonConstructorResolver = infoJsonConstructorResolver;
        this.orchestratorSettings = orchestratorSettings.Value;
        this.logger = logger;
    }

    public async Task<InfoJsonResponse?> GetInfoJson(OrchestrationImage orchestrationImage,
        Version version, CancellationToken cancellationToken = default)
    {
        var infoJsonKey = GetInfoJsonKey(orchestrationImage, version);
        await using var infoJson = await GetStoredInfoJson(infoJsonKey, cancellationToken);

        if (!infoJson.IsNull())
        {
            // If info.json found in S3, return it
            JsonLdBase deserialisedInfoJson = version == Version.V2
                ? infoJson.FromJsonStream<ImageService2>()
                : infoJson.FromJsonStream<ImageService3>();

            logger.LogTrace("Found info.json version {Version} for {AssetId}", version, orchestrationImage.AssetId);
            return GetInfoJsonResponse(deserialisedInfoJson, false);
        }

        // If not found, build new copy
        var infoJsonConstructor = infoJsonConstructorResolver(version);
        var infoJsonResponse =
            await infoJsonConstructor.BuildInfoJsonFromImageServer(orchestrationImage, cancellationToken);

        if (infoJsonResponse == null) return null;

        await StoreInfoJson(infoJsonKey, infoJsonResponse, cancellationToken);
        return GetInfoJsonResponse(infoJsonResponse, true);
    }

    private InfoJsonResponse GetInfoJsonResponse(JsonLdBase infoJsonResponse, bool wasOrchestrated)
    {
        if (infoJsonResponse is ImageService2 imageService2)
        {
            imageService2.Type = DLCS.Model.IIIF.Constants.ImageService2Type;
        }
        
        return new InfoJsonResponse(infoJsonResponse, wasOrchestrated);
    }

    private ObjectInBucket GetInfoJsonKey(OrchestrationImage asset, Version version)
    {
        var imageServer = orchestratorSettings.ImageServer.ToString();
        var infoJsonCandidate = storageKeyGenerator.GetInfoJsonLocation(asset.AssetId, imageServer, version);
        return infoJsonCandidate;
    }

    private async Task<Stream?> GetStoredInfoJson(ObjectInBucket infoJsonKey, CancellationToken cancellationToken)
    {
        var objectFromBucket = await bucketReader.GetObjectFromBucket(infoJsonKey, cancellationToken);
        var oldestAllowedInfoJson = orchestratorSettings.OldestAllowedInfoJson;
        
        if (!oldestAllowedInfoJson.HasValue) return objectFromBucket.Stream;
        if (oldestAllowedInfoJson.Value < objectFromBucket.Headers.LastModified) return objectFromBucket.Stream;
        
        logger.LogDebug("info.json from key '{InfoJsonKey}' too old ({LastModified}), regenerating",
            infoJsonKey.ToString(), objectFromBucket.Headers.LastModified);
        return null;
    }

    private Task StoreInfoJson(ObjectInBucket infoJsonKey, JsonLdBase infoJson, CancellationToken cancellationToken)
        => bucketWriter.WriteToBucket(infoJsonKey, infoJson.AsJson(), "application/json", cancellationToken);
}