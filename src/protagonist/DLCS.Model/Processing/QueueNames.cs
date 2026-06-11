namespace DLCS.Model.Processing;

public static class QueueNames
{
    /// <summary>
    /// Name of the default queue, used for standard processing of assets
    /// </summary>
    public const string Default = "default";
    
    /// <summary>
    /// Name of the priority queue, this is the same processing as default queue but will be handled quicker
    /// </summary>
    public const string Priority = "priority";
    
    /// <summary>
    /// Name of the queue for processing adjuncts
    /// </summary>
    public const string Adjunct = "adjunct";
}
