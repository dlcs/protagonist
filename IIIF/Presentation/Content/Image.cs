namespace IIIF.Presentation.Content
{
    public class Image : ExternalResource, ISpatial
    {
        public int Width { get; set; }
        public int Height { get; set; }

        public Image() : base(nameof(Image)) {}
    }
}
