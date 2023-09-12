using API.Infrastructure.Requests;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Storage.Requests;

public class GetImageStorage : IRequest<FetchEntityResult<ImageStorage>>
{
    public int CustomerId { get; }
    
    public int SpaceId { get; }
    
    public string ImageId { get; }
    
    public GetImageStorage(int customerId, int spaceId, string imageId)
    {
        CustomerId = customerId;
        SpaceId = spaceId;
        ImageId = imageId;
    }
}

public class GetImageStorageHandler : IRequestHandler<GetImageStorage, FetchEntityResult<ImageStorage>>
{
    private readonly DlcsContext dbContext;

    public GetImageStorageHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<FetchEntityResult<ImageStorage>> Handle(GetImageStorage request, CancellationToken cancellationToken)
    {
        var assetId = new AssetId(request.CustomerId, request.SpaceId, request.ImageId);
        var storage = await dbContext.ImageStorages.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Customer == request.CustomerId && s.Id == assetId,
                cancellationToken: cancellationToken);
        
        return storage == null
            ? FetchEntityResult<ImageStorage>.NotFound()
            : FetchEntityResult<ImageStorage>.Success(storage);
    }
}