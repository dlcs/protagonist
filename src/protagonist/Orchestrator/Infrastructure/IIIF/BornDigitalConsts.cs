namespace Orchestrator.Infrastructure.IIIF;

/// <summary>
/// Constants related to born-digital content, originally proposed as part of RFC for Wellcome collection work.
/// See https://github.com/wellcomecollection/docs/tree/main/rfcs/046-born-digital-iiif
/// </summary>
public class BornDigitalConsts
{
    /// <summary>
    /// IIIF Context to use when using any of the behaviours listed here
    /// </summary>
    public const string Context = "https://iiif.wellcomecollection.org/extensions/born-digital/context.json";

    public const string PlaceholderBehavior = "placeholder";
    public const string OriginalBehavior = "original";
}
