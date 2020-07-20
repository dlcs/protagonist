
namespace IIIF.Presentation.Selectors
{
    public class ImageApiSelector : ISelector
    {
        public string? Type => nameof(ImageApiSelector);
        public string? Region { get; set; }
        public string? Size { get; set; }
        public string? Rotation { get; set; }
        public string? Quality { get; set; }
        public string? Format { get; set; }
    }
}
