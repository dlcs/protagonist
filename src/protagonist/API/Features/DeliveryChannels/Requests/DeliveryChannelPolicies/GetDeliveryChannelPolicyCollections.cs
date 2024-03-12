using System.Collections.Generic;
using DLCS.Model.Assets;
using DLCS.Repository;
using MediatR;

namespace API.Features.DeliveryChannels.Requests.DeliveryChannelPolicies;

public class GetDeliveryChannelPolicyCollections: IRequest<Dictionary<string,string>>
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

public class GetDeliveryChannelPolicyCollectionsHandler : IRequestHandler<GetDeliveryChannelPolicyCollections, Dictionary<string,string>>
{
    private readonly DlcsContext dbContext;
    
    public GetDeliveryChannelPolicyCollectionsHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<Dictionary<string,string>> Handle(GetDeliveryChannelPolicyCollections request, CancellationToken cancellationToken)
    {
        var policyCollections = new Dictionary<string, string>()
        {
            {AssetDeliveryChannels.Image, "Policies for IIIF Image service delivery"},
            {AssetDeliveryChannels.Thumbnails, "Policies for thumbnails as IIIF Image Services"},
            {AssetDeliveryChannels.Timebased, "Policies for Audio and Video delivery"},
            {AssetDeliveryChannels.File, "Policies for File delivery"}
        };

        return policyCollections;
    }
}