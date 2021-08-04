namespace Orchestrator.Assets
{
    /// <summary>
    /// Represents an asset during orchestration.
    /// </summary>
    public class TrackedAsset
    {
        public string AssetId { get; set; }
        public bool RequiresAuth { get; set; }
        
        // TODO - this will manage the state of the Asset (Orchestrated, Orchestrating, Not-Orchestrated)
    }
}