using System;
using DLCS.Core.Types;
using IIIF.Presentation.V3.Strings;

namespace DLCS.Model.Assets;

public class Adjunct
{
    /// <summary>
    /// Model id of the adjunct
    /// </summary>
    public required string Id { get; set; }
    
    /// <summary>
    /// The internet content type (or MIME type) of the resource
    /// </summary>
    public required string MediaType { get; set; }
    
    /// <summary>
    /// How this adjunct is expressed in IIIF presentation
    /// </summary>
    public required IiifLinkType IiifLink { get; set; }
    
    /// <summary>
    /// The asset this adjunct is associated with
    /// </summary>
    public required AssetId AssetId { get; set; }
    
    /// <summary>
    /// A schema or named set of functionality available from the resource
    /// </summary>
    public string? Profile { get; set; }
    
    /// <summary>
    /// A human readable label, name or title
    /// </summary>
    public LanguageMap? Label { get; set; }
    
    /// <summary>
    /// The language(s) of the content
    /// </summary>
    public string[]? Language { get; set; }
    
    /// <summary>
    /// A fully-qualified URL external to the platform
    /// </summary>
    public Uri? ExternalId { get; set; }
    
    /// <summary>
    /// When the adjunct was created
    /// </summary>
    public DateTime Created { get; set; }
    
    /// <summary>
    /// When the adjunct was modified
    /// </summary>
    public DateTime Modified { get; set; }
    
    /// <summary>
    /// The size in bytes of the adjunct
    /// </summary>
    public long? Size { get; set; }

    public Asset Asset { get; set; } = null!;
}

public enum IiifLinkType
{
    SeeAlso,
    Annotations,
    Rendering
}
