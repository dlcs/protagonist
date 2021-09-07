using System;
using API.Client.JsonLd;

namespace Portal.Features.Spaces.Models
{
    public class SpacePageModel
    {
        public Space? Space { get; set; }
        public HydraImageCollection? Images { get; set; }
        
        public Uri? UniversalViewer { get; set; }
        
        public Uri? NamedQuery { get; set; }
        
        public Uri? MiradorViewer { get; set; }

        public bool IsManifestSpace { get; set; }
    }
}