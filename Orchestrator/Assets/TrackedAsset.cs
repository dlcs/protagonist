using DLCS.Core.Types;

namespace Orchestrator.Assets
{
    /// <summary>
    /// Represents an asset during orchestration.
    /// </summary>
    public class TrackedAsset
    {
        public AssetId AssetId { get; set; }
        public bool RequiresAuth { get; set; }
        public string Origin { get; set; }
        
        // TODO - this will manage the state of the Asset (Orchestrated, Orchestrating, Not-Orchestrated)
    }
}