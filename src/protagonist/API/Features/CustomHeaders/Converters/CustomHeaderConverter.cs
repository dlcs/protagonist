namespace API.Features.CustomHeaders.Converters;

public static class CustomHeaderConverter
{
    public static DLCS.HydraModel.CustomHeader ToHydra(this DLCS.Model.Assets.CustomHeaders.CustomHeader customHeader, string baseUrl)
    {
        return new DLCS.HydraModel.CustomHeader(baseUrl, customHeader.Customer, customHeader.Id, false)
        {
            SpaceId = customHeader.Space,
            Role = customHeader.Role,
            Key = customHeader.Key,
            Value = customHeader.Value,
        };
    }
    
    public static DLCS.Model.Assets.CustomHeaders.CustomHeader ToDlcsModel(this DLCS.HydraModel.CustomHeader hydraNamedQuery)
    {
        return new DLCS.Model.Assets.CustomHeaders.CustomHeader()
        {
            Id = hydraNamedQuery.ModelId,
            Customer = hydraNamedQuery.CustomerId,
            Space = hydraNamedQuery.SpaceId,
            Role = hydraNamedQuery.Role,
            Key = hydraNamedQuery.Key,
            Value = hydraNamedQuery.Value
        };
    }
}