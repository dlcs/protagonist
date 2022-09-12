using Amazon.SQS;
using DLCS.Core;
using Microsoft.Extensions.Logging;

namespace DLCS.AWS.SQS;

/// <summary>
/// Implementation of <see cref="IQueueSender"/> using Sqs for backing queue
/// </summary>
public class SqsQueueSender : IQueueSender
{
    private readonly IAmazonSQS client;
    private readonly SqsQueueUtilities queueUtilities;
    private readonly ILogger<SqsQueueSender> logger;
    private readonly Dictionary<string, string> nameUrlLookup = new();

    public SqsQueueSender(IAmazonSQS client, SqsQueueUtilities queueUtilities, ILogger<SqsQueueSender> logger)
    {
        this.client = client;
        this.queueUtilities = queueUtilities;
        this.logger = logger;
    }

    public async Task<bool> QueueMessage(string queueName, string messageContents,
        CancellationToken cancellationToken = default)
    {
        var queueUrl = await QueueLookup.GetQueueUrl(queueUtilities, queueName, cancellationToken);
        try
        {
            var result = await client.SendMessageAsync(queueUrl, messageContents, cancellationToken);
            return result.HttpStatusCode.IsSuccess();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending message to {QueueName}", queueName);
            return false;
        }
    }
}