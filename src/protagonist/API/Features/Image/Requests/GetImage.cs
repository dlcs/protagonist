using API.Features.Assets;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using MediatR;

namespace API.Features.Image.Requests;

/// <summary>
/// Get asset with provided Id
/// </summary>
public class GetImage : IRequest<Asset?>
{
    public GetImage(AssetId assetId, bool noCache)
    {
        AssetId = assetId;
        NoCache = noCache;
    }
    
    public AssetId AssetId { get; }
    public bool NoCache { get; }
}

public class GetImageHandler : IRequestHandler<GetImage, Asset?>
{
    private readonly IApiAssetRepository assetRepository;

    public GetImageHandler(IApiAssetRepository assetRepository)
    {
        this.assetRepository = assetRepository;
    }
    
    public async Task<Asset?> Handle(GetImage request, CancellationToken cancellationToken)
    {
        var image = await assetRepository.GetAsset(request.AssetId, noCache: request.NoCache);
        return image;
    }
}
