using API.Exceptions;
using API.Features.Assets;
using API.Infrastructure.Requests;
using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.Transcoding.Models.Job;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using MediatR;

namespace API.Features.Image.Requests;

/// <summary>
/// Get metadata associated with external processing of asset 
/// </summary>
/// <remarks>
/// This is NOT string1, number2 etc but metadata associated with external processing of asset, e.g. elastictranscoder
/// </remarks>
public class GetAssetMetadata : IRequest<FetchEntityResult<TranscoderJob>>
{
    public AssetId AssetId { get; }

    public GetAssetMetadata(int customerId, int spaceId, string assetId)
    {
        AssetId = new AssetId(customerId, spaceId, assetId);
    }
}

public class GetAssetMetadataHandler : IRequestHandler<GetAssetMetadata, FetchEntityResult<TranscoderJob>>
{
    private readonly IApiAssetRepository assetRepository;
    private readonly IElasticTranscoderWrapper elasticTranscoderWrapper;

    public GetAssetMetadataHandler(
        IApiAssetRepository assetRepository, 
        IElasticTranscoderWrapper elasticTranscoderWrapper)
    {
        this.assetRepository = assetRepository;
        this.elasticTranscoderWrapper = elasticTranscoderWrapper;
    }
    
    public async Task<FetchEntityResult<TranscoderJob>> Handle(GetAssetMetadata request, CancellationToken cancellationToken)
    {
        var asset = await assetRepository.GetAsset(request.AssetId);
        
        if (asset == null) return FetchEntityResult<TranscoderJob>.NotFound();

        if (asset.Family != AssetFamily.Timebased)
        {
            throw new BadRequestException("Can only get metadata for Timebased asset");
        }

        var transcoderJob = await elasticTranscoderWrapper.GetTranscoderJob(request.AssetId, cancellationToken);
        return transcoderJob == null
            ? FetchEntityResult<TranscoderJob>.NotFound()
            : FetchEntityResult<TranscoderJob>.Success(transcoderJob);
    }
}

