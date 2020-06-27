using IIIF.Presentation.Content;

namespace IIIF.Presentation
{
    public class Agent : ResourceBase
    {
        public override string Type => nameof(Agent);
        public Image[]? Logo { get; set; }
    }
}
