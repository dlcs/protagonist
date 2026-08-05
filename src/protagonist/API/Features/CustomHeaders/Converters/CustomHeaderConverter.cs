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
    public static DLCS.Model.Assets.CustomHeaders.CustomHeader ToDlcsModel(this DLCS.HydraModel.CustomHeader hydraCustomHeader)
    {
        return new DLCS.Model.Assets.CustomHeaders.CustomHeader()
        {
            Id = hydraCustomHeader.ModelId,
            Customer = hydraCustomHeader.CustomerId,
            Space = hydraCustomHeader.SpaceId,
            Role = hydraCustomHeader.Role,
            Key = hydraCustomHeader.Key,
            Value = hydraCustomHeader.Value
        };
    }
}