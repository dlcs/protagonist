using DLCS.Model.Assets;
using DLCS.Model.Policies;
using DLCS.Repository;
using Hydra.Collections;
using MediatR;

namespace API.Features.DeliveryChannels.Requests;

public class GetDeliveryChannelPolicyCollections: IRequest<HydraCollection<HydraNestedCollection<DeliveryChannelPolicy>>>
{
    public int CustomerId { get; }
    public string BaseUrl { get; }
    public string JsonLdId { get; }
    
    public GetDeliveryChannelPolicyCollections(int customerId, string baseUrl, string jsonLdId)
    {
        CustomerId = customerId;
        BaseUrl = baseUrl;
        JsonLdId = jsonLdId;
    }
}

public class GetDeliveryChannelPolicyCollectionsHandler : IRequestHandler<GetDeliveryChannelPolicyCollections, HydraCollection<HydraNestedCollection<DeliveryChannelPolicy>>>
{
    private readonly DlcsContext dbContext;
    
    public GetDeliveryChannelPolicyCollectionsHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<HydraCollection<HydraNestedCollection<DeliveryChannelPolicy>>> Handle(GetDeliveryChannelPolicyCollections request, CancellationToken cancellationToken)
    {
        var policyCollections = new HydraNestedCollection<DeliveryChannelPolicy>[]
        {
            new(request.BaseUrl, AssetDeliveryChannels.Image)
            {
                Title = "Policies for IIIF Image service delivery",
            },
            new(request.BaseUrl, AssetDeliveryChannels.Thumbnails)
            {
                Title = "Policies for thumbnails as IIIF Image Services",
            },
            new(request.BaseUrl, AssetDeliveryChannels.Timebased)
            {
                Title = "Policies for Audio and Video delivery",
            },
            new(request.BaseUrl, AssetDeliveryChannels.File)
            {
                Title = "Policies for File delivery",
            }
        };
        
        return new HydraCollection<HydraNestedCollection<DeliveryChannelPolicy>>()
        {
            WithContext = true,
            Members = policyCollections,
            TotalItems = policyCollections.Length,
            Id = request.JsonLdId,
        };
    }
}