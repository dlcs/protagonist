namespace IIIF.Presentation.Content
{
    public class Video : ExternalResource, ISpatial, ITemporal
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public double Duration { get; set; }

        public Video() : base(nameof(Video)) { }
    }
}