namespace IIIF.Presentation.Annotation
{
    public class Annotation : ResourceBase, IAnnotation
    {
        public override string Type => nameof(Annotation);
        public string? TimeMode { get; set; }
    }
}
