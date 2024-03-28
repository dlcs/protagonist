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
    public GetImage(AssetId assetId)
    {
        AssetId = assetId;
    }
    
    public AssetId AssetId { get; }
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
        var image = await assetRepository.GetAsset(request.AssetId);
        return image;
    }
}