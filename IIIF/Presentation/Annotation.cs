namespace IIIF.Presentation
{
    public class Annotation : ResourceBase, IAnnotation
    {
        public override string Type => nameof(Annotation);
        public string? TimeMode { get; set; }
    }
}
