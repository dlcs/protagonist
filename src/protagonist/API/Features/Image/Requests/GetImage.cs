using API.Features.Assets;
using API.Infrastructure.Requests;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using MediatR;

namespace API.Features.Image.Requests;

/// <summary>
/// Get asset with provided Id
/// </summary>
public class GetImage(AssetId assetId, bool noCache) : IRequest<FetchEntityResult<Asset>>
{
    public AssetId AssetId { get; } = assetId;
    public bool NoCache { get; } = noCache;
}

public class GetImageHandler(IApiAssetRepository assetRepository) : IRequestHandler<GetImage, FetchEntityResult<Asset>>
{
    public async Task<FetchEntityResult<Asset>> Handle(GetImage request, CancellationToken cancellationToken)
    {
        var asset = await assetRepository.GetAsset(request.AssetId, noCache: request.NoCache);
        return asset == null
            ? FetchEntityResult<Asset>.NotFound()
            : FetchEntityResult<Asset>.Success(asset);
    }
}
