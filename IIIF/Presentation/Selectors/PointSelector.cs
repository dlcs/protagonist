namespace IIIF.Presentation.Selectors
{
    public class PointSelector : ISelector
    {
        public string? Type => nameof(PointSelector);
        public int? X { get; set; }
        public int? Y { get; set; }
        public double? T { get; set; }
    }
}
