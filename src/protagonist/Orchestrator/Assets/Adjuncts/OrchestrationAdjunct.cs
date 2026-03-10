using DLCS.Core.Types;
using DLCS.Model.Assets;
using Microsoft.Extensions.Primitives;

namespace Orchestrator.Assets;

public class OrchestrationAdjunct : IOriginItem
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
    /// Currently adjuncts are not auth-covered, this always returns <c>false</c>
    /// </summary>
    public bool RequiresAuth => false;

    /// <inheritdoc/>
    public string Identifier() => $"adjunct '{Id}' for asset '{AssetId}'";

}
