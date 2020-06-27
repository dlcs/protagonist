namespace IIIF.Presentation.Content
{
    public class Audio : ExternalResource, ITemporal
    {
        public double Duration { get; set; }

        public Audio() : base(nameof(Image)) { }
    }
}