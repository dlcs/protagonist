using System.Linq;
using IIIF;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;

namespace Test.Helpers;

public static class ManifestHelpers
{
    /// <summary>
    /// Get canvas.items[0].items[0].body
    /// </summary>
    public static T GetCanvasPaintingBody<T>(this Canvas canvas)
        where T : class, IPaintable 
        => canvas.Items!.Single().Items!.OfType<PaintingAnnotation>().Single().Body as T;

    /// <summary>
    /// Get service[0] from resource
    /// </summary>
    public static T GetService<T>(this ResourceBase resourceBase)
        where T : IService
        => resourceBase.Service!.OfType<T>().Single();

    /// <summary>
    /// Get thumbnail[0] from resource, ensuring only 1
    /// </summary>
    public static ExternalResource GetSingleThumbnail(this ResourceBase resourceBase)
        => resourceBase.Thumbnail!.Single();

}
