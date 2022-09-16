namespace DLCS.AWS.SQS;

internal static class QueueLookup
{
    private static readonly Dictionary<string, string> NameUrlLookup = new();
    
    public static async ValueTask<string> GetQueueUrl(SqsQueueUtilities queueUtilities, string queueName, 
        CancellationToken cancellationToken)
    {
        if (NameUrlLookup.TryGetValue(queueName, out var dictQueueUrl)) return dictQueueUrl;

        var queueUrl = await queueUtilities.GetQueueUrl(queueName, cancellationToken);
        NameUrlLookup[queueName] = queueUrl;
        return queueUrl;
    }
}