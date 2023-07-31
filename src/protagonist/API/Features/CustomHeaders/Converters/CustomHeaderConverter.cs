namespace API.Features.CustomHeaders.Converters;

public static class CustomHeaderConverter
{
    public static DLCS.HydraModel.CustomHeader ToHydra(this DLCS.Model.Assets.CustomHeaders.CustomHeader customHeader, string baseUrl)
    {
        return new DLCS.HydraModel.CustomHeader(baseUrl, customHeader.Customer, customHeader.Id, false)
        {
            Role = customHeader.Role,
            Key = customHeader.Key,
            Value = customHeader.Value,
        };
    }
}