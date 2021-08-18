using System.Collections.Generic;
using DLCS.Core.Types;

namespace Orchestrator.Assets
{
    /// <summary>
    /// Represents an asset during orchestration.
    /// </summary>
    public class OrchestrationAsset
    {
        /// <summary>
        /// Get or set the AssetId for tracked Asset
        /// </summary>
        public AssetId AssetId { get; set; }
        
        /// <summary>
        /// Get or set boolean indicating whether asset is restricted or not.
        /// </summary>
        public bool RequiresAuth { get; set; }
        
        /// <summary>
        /// Get or set Asset origin 
        /// </summary>
        public string Origin { get; set; }

        // TODO - this will manage the state of the Asset (Orchestrated, Orchestrating, Not-Orchestrated)
    }

    public class OrchestrationImage : OrchestrationAsset
    {
        /// <summary>
        /// Get or set asset Width
        /// </summary>
        public int Width { get; set; }
        
        /// <summary>
        /// Get or set asset Height
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets list of thumbnail sizes
        /// </summary>
        public List<int[]> OpenThumbs { get; set; } = new();
    }
}