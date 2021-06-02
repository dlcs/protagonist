using API.JsonLd;

namespace Portal.Features.Spaces.Models
{
    public class SpacePageModel
    {
        public Space? Space { get; set; }
        public HydraImageCollection? Images { get; set; }
    }
}