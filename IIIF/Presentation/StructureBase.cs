using IIIF.Presentation.Annotation;
using System;
using System.Collections.Generic;

namespace IIIF.Presentation
{
    /// <summary>
    /// Base class for resources that form the structure of a IIIF resource:
    /// Manifest, Canvas, Collection, Range
    /// </summary>
    public abstract class StructureBase : ResourceBase
    {
        private DateTime? navDateInternal;

        /// <summary>
        /// You can't always set a navDate as a DateTime/
        /// Not serialised.
        /// </summary>
        public DateTime? NavDateDateTime 
        {
            get
            {
                return navDateInternal;
            }
            set
            {
                navDateInternal = value;
                if(navDateInternal == null)
                {
                    NavDate = null;
                }
                else
                {
                    NavDate = navDateInternal.Value.ToString("o");
                }
            }
        }

        /// <summary>
        /// This can still be set manually
        /// </summary>
        public string? NavDate { get; set; }

        public Canvas? PlaceholderCanvas { get; set; }
        public Canvas? AccompanyingCanvas { get; set; }

        public List<AnnotationPage>? Annotations { get; set; }
    }
}
