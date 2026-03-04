using System;
using System.ComponentModel;
using DLCS.Core.Types;
using IIIF.Presentation.V3.Strings;

namespace DLCS.Model.Assets;

public class Adjunct : IDeliverable
{
    public const int MaxIdLength = 200;
    
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
    public required IIIFLinkType IIIFLink { get; set; }
    
    /// <summary>
    /// The asset id this adjunct is associated with
    /// </summary>
    public required AssetId AssetId { get; set; }
    
    /// <summary>
    /// The type of the adjunct
    /// </summary>
    public required string Type { get; set; }
    
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
    /// A fully-qualified URL external to the platform where the adjunct is hosted
    /// </summary>
    public Uri? ExternalId { get; set; }
    
    /// <inheritdoc/>
    public bool? Ingesting { get; set; }
    
    /// <inheritdoc/>
    public string? Error { get; set; }

    public string? Origin { get; set; }

    /// <summary>
    /// When the adjunct was created
    /// </summary>
    public DateTime Created { get; set; }
    
    /// <summary>
    /// When the adjunct last finished processing
    /// </summary>
    public DateTime? Finished { get; set; }

    /// <inheritdoc/>
    public string ItemId => Id;
    
    /// <inheritdoc/>
    public string Identifier() => $"adjunct '{Id}' for asset '{AssetId}'";

    /// <inheritdoc/>
    public AssetId GetAssetId() => AssetId;
    
    /// <summary>
    /// The size in bytes of the adjunct
    /// </summary>
    public long? Size { get; set; }

    public Asset Asset { get; set; } = null!;
}

/// <summary>
/// The type of linking property to use for adjunct. Determines how this is output on Manifest.
/// </summary>
public enum IIIFLinkType
{
    [Description("seeAlso")]
    SeeAlso,
    [Description("annotations")]
    Annotations,
    [Description("rendering")]
    Rendering
}
