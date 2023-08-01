namespace API.Features.CustomHeaders.Converters;

/// <summary>
/// Conversion between API and EF forms of CustomHeader resource
/// </summary>
public static class CustomHeaderConverter
{
    /// <summary>
    /// Convert CustomHeader entity to API resource
    /// </summary>
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
    
    /// <summary>
    /// Convert Hydra CustomHeader entity to EF resource
    /// </summary>
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