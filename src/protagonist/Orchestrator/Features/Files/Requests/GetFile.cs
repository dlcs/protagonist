using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Streams;
using DLCS.Core.Types;
using DLCS.Model.Customers;
using DLCS.Repository.Strategy;
using DLCS.Repository.Strategy.Utils;
using DLCS.Web.Requests.AssetDelivery;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.Mediatr;
using Orchestrator.Settings;

namespace Orchestrator.Features.Files.Requests;

/// <summary>
/// Mediatr request for loading file from origin
/// </summary>
public class GetFile : IRequest<OriginResponse>, IFileRequest
{
    public string FullPath { get; }
    
    public FileAssetDeliveryRequest? AssetRequest { get; set; }

    public GetFile(string path)
    {
        FullPath = path;
    }
}

public class GetFileHandler : IRequestHandler<GetFile, OriginResponse>
{
    private readonly IAssetTracker assetTracker;
    private readonly OriginFetcher originFetcher;
    private readonly ICustomerOriginStrategyRepository customerOriginStrategyRepository;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly S3AmbientOriginStrategy s3OriginStrategy;
    private readonly OrchestratorSettings orchestratorSettings;
    private readonly ILogger<GetFileHandler> logger;

    public GetFileHandler(
        IAssetTracker assetTracker, 
        OriginFetcher originFetcher, 
        ICustomerOriginStrategyRepository customerOriginStrategyRepository,
        IStorageKeyGenerator storageKeyGenerator,
        IOptions<OrchestratorSettings> orchestratorOptions,
        S3AmbientOriginStrategy s3OriginStrategy,
        ILogger<GetFileHandler> logger)
    {
        this.assetTracker = assetTracker;
        this.originFetcher = originFetcher;
        this.customerOriginStrategyRepository = customerOriginStrategyRepository;
        this.storageKeyGenerator = storageKeyGenerator;
        this.s3OriginStrategy = s3OriginStrategy;
        orchestratorSettings = orchestratorOptions.Value;
        this.logger = logger;
    }

    public async Task<OriginResponse> Handle(GetFile request, CancellationToken cancellationToken)
    {
        var assetId = request.AssetRequest.GetAssetId();
        var asset = await assetTracker.GetOrchestrationAsset<OrchestrationAsset>(assetId);
        if (!IsAssetAvailableToServe(assetId, asset))
        {
            return OriginResponse.Empty;
        }

        // Get the origin-strategy for the 'origin' - this is required to know whether file in DLCS or origin
        var strategyForOrigin = await customerOriginStrategyRepository.GetCustomerOriginStrategy(assetId, asset.Origin!);
        
        var assetFromOrigin = await GetFile(strategyForOrigin, asset, cancellationToken);

        var fileShouldBeInDlcsStorage = !strategyForOrigin.Optimised;
        if (assetFromOrigin.Stream.IsNull() && fileShouldBeInDlcsStorage && orchestratorSettings.StreamMissingFileFromOrigin)
        {
            logger.LogWarning("Asset {Asset} not found in DLCS, streaming from origin", asset.AssetId);
            assetFromOrigin = await LoadFileFromOrigin(asset, strategyForOrigin, cancellationToken);
        }

        return assetFromOrigin;
    }

    private bool IsAssetAvailableToServe(AssetId assetId, [NotNullWhen(true)] OrchestrationAsset? asset)
    {
        if (asset == null)
        {
            logger.LogDebug("Asset {AssetId} not found, or not available for delivery", assetId);
            return false;
        }
        
        if (!asset.Channels.HasFlag(AvailableDeliveryChannel.File))
        {
            logger.LogDebug("Asset {AssetId} not available for file delivery-channel", assetId);
            return false;
        }

        if (string.IsNullOrEmpty(asset.Origin))
        {
            // Note - this shouldn't ever happen but the property is nullable
            logger.LogDebug("Asset {AssetId} has no origin set", assetId);
            return false;
        }

        return true;
    }

    private async Task<OriginResponse> GetFile(CustomerOriginStrategy cos, OrchestrationAsset asset,
        CancellationToken cancellationToken)
    {
        if (cos.Optimised)
        {
            logger.LogDebug("File asset {Asset} is at optimised origin, streaming from origin: {FileLocation}", asset.AssetId,
                asset.Origin);
            return await LoadFileFromOrigin(asset, cos, cancellationToken);
        }

        // Origin is not optimised so a copy should be in S3
        return await LoadFileFromDlcs(asset, cancellationToken);
    }
    
    private async Task<OriginResponse> LoadFileFromOrigin(OrchestrationAsset asset, CustomerOriginStrategy cos,
        CancellationToken cancellationToken)
    {
        var response = await originFetcher.LoadAssetFromLocation(asset.AssetId, asset.Origin!, cos, cancellationToken);
        return response;
    }

    private async Task<OriginResponse> LoadFileFromDlcs(OrchestrationAsset asset, CancellationToken cancellationToken)
    {
        var objectInBucket = storageKeyGenerator.GetStoredOriginalLocation(asset.AssetId);
        var fileLocation = objectInBucket.GetHttpUri().ToString();
        logger.LogDebug("File asset {Asset} not at optimised origin, streaming from DLCS: {FileLocation}", asset.AssetId,
            fileLocation);
        var response = await s3OriginStrategy.LoadAssetFromOrigin(asset.AssetId, fileLocation, null, cancellationToken);
        return response;
    }
}