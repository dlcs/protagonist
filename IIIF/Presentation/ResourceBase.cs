using IIIF.Presentation.Content;
using IIIF.Presentation.Services;
using IIIF.Presentation.Strings;
using System.Collections.Generic;

namespace IIIF.Presentation
{
    public abstract class ResourceBase
    {
        public object? Context { get; set; } // This one needs its serialisation name changing...
        public abstract string Type { get; } 
        public string? Id { get; set; }
        public LanguageMap? Label { get; set; }
        public LanguageMap? Summary { get; set; }
        public List<LabelValuePair>? Metadata { get; set; }
        public LabelValuePair? RequiredStatement { get; set; }
        public string? Rights { get; set; }
        public List<Agent>? Provider { get; set; }
        public List<ExternalResource>? Thumbnail { get; set; }
        public string? Profile { get; set; }
        public List<string>? Behavior { get; set; }
        public List<ExternalResource>? HomePage { get; set; }
        public List<ExternalResource>? Rendering { get; set; }
        public List<IService>? Service { get; set; }
        public List<ExternalResource>? SeeAlso { get; set; }
        public List<ResourceBase>? PartOf { get; set; }
    }
}
