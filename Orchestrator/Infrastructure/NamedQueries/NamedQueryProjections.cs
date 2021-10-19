using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;

namespace Orchestrator.Infrastructure.NamedQueries
{
    internal static class NamedQueryProjections
    {
        public static object GetCanvasOrderingElement(Asset image, ParsedNamedQuery query)
            => query.Canvas switch
            {
                ParsedNamedQuery.QueryMapping.Number1 => image.NumberReference1,
                ParsedNamedQuery.QueryMapping.Number2 => image.NumberReference2,
                ParsedNamedQuery.QueryMapping.Number3 => image.NumberReference3,
                ParsedNamedQuery.QueryMapping.String1 => image.Reference1,
                ParsedNamedQuery.QueryMapping.String2 => image.Reference2,
                ParsedNamedQuery.QueryMapping.String3 => image.Reference3,
                _ => 0
            };
    }
}