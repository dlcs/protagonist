using API.Exceptions;

namespace API.Features.NamedQueries.Converters;

public static class NamedQueryConverter
{
    public static DLCS.HydraModel.NamedQuery ToHydra(this DLCS.Model.Assets.NamedQueries.NamedQuery namedQuery, string baseUrl)
    {
        var hydraNamedQuery = new DLCS.HydraModel.NamedQuery(baseUrl, namedQuery.Customer, namedQuery.Id)
        {
            Name = namedQuery.Name,
            Template = namedQuery.Template,
            Global = namedQuery.Global
        };
        return hydraNamedQuery;
    }
    
    public static DLCS.Model.Assets.NamedQueries.NamedQuery ToDlcsModel(this DLCS.HydraModel.NamedQuery hydraNamedQuery)
    {
        var namedQuery = new DLCS.Model.Assets.NamedQueries.NamedQuery()
        {
            Id = hydraNamedQuery.ModelId,
            Customer = hydraNamedQuery.CustomerId,
            Name = hydraNamedQuery.Name,
            Template = hydraNamedQuery.Template,
        };
        return namedQuery;
    }
}