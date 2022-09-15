namespace DLCS.Model.Processing;

public class Queue
{
    public int Customer { get; set; }
    
    /// <summary>
    /// Approximate number of items currently in-flight in queue.
    /// Includes all family items waiting in ingest queues (ie not in queues like 'transcode complete')
    /// </summary>
    /// <remarks>
    /// This is 'approximate' as it doesn't directly track items in SQS, it's incremented when items are added
    /// and decremented when removed but if items are removed outwith the DLCS then this count wouldn't know
    /// </remarks>
    public int Size { get; set; }
    
    public string Name { get; set; }
}