namespace Orchestrator.Infrastructure.NamedQueries.Persistence.Models;

/// <summary>
/// Basic result to represent success/failure of operation to create NQ project
/// </summary>
public class CreateProjectionResult
{
    /// <summary>
    /// Indicator of success for stored named query project
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Size, in bytes, of generated resource.
    /// </summary>
    public long Size { get; set; }
}