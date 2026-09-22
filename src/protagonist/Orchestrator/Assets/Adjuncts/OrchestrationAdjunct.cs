using System.Collections.Generic;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using Microsoft.Extensions.Primitives;

namespace Orchestrator.Assets;

public class OrchestrationAdjunct : IOriginItem, IProbeableOrchestrationItem
{
    /// <summary>
    /// Model id of the adjunct
    /// </summary>
    public required string Id { get; set; }
    
    /// <summary>
    /// The asset id this adjunct is associated with
    /// </summary>
    public required AssetId AssetId { get; set; }
    
    /// <summary>
    /// How this adjunct is expressed in IIIF presentation
    /// </summary>
    public IIIFLinkType IIIFLink { get; set; }

    /// <summary>
    /// Get or set the adjunct media-type
    /// </summary>
    public StringValues? MediaType { get; set; }
    
    /// <summary>
    /// Get or set whether this adjunct has an optimised origin 
    /// </summary>
    /// <remarks>Optimised adjuncts might be served directly</remarks>
    public bool? OptimisedOrigin { get; set; }
    
    /// <inheritdoc/>
    public string? Origin { get; set; }
    
    /// <inheritdoc/>
    public string ItemId => Id;

    /// <summary>
    /// Gets list of roles associated with this adjunct, inherited from the parent Asset
    /// </summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// Get boolean indicating whether this adjunct is restricted, based on parent Asset roles
    /// </summary>
    public bool RequiresAuth => Roles.Count > 0;

    /// <inheritdoc/>
    public string Identifier() => $"adjunct '{Id}' for asset '{AssetId}'";

}
