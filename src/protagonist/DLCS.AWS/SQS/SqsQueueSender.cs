using Amazon.SQS;
using Amazon.SQS.Model;
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

    public async Task<int> QueueMessages(string queueName, IReadOnlyCollection<string> messageContents,
        string batchIdentifier, CancellationToken cancellationToken = default)
    {
        const int batchSize = 10;
        var queueUrl = await QueueLookup.GetQueueUrl(queueUtilities, queueName, cancellationToken);
        int successCount = 0;
        try
        {
            int batchCount = 0;
            int count = 0;
            foreach (var batch in messageContents.Chunk(batchSize))
            {
                var batchPrefix = $"{batchIdentifier}_{++batchCount}";
                var entries = batch
                    .Select(c => new SendMessageBatchRequestEntry($"{batchPrefix}_{++count}", c))
                    .ToList();
                var batchResult = await client.SendMessageBatchAsync(queueUrl, entries, cancellationToken);
                
                if (!batchResult.HttpStatusCode.IsSuccess())
                {
                    logger.LogError("Overall batch failure for {BatchPrefix}. StatusCode: {StatusCode}", batchPrefix,
                        batchResult.HttpStatusCode);
                }
                
                if (batchResult.Failed.Count > 0)
                {
                    foreach (var errorEntry in batchResult.Failed)
                    {
                        logger.LogError("Failed message {MessageId}, message: {Error}", errorEntry.Id,
                            errorEntry.Message);
                    }
                }

                successCount += batchResult.Successful.Count;
            }

            return successCount;
        }
        catch (BatchRequestTooLongException)
        {
            logger.LogError("Batch {BatchIdentifier} too long. Batch size: {BatchSize}", batchIdentifier, batchSize);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending message to {QueueName}", queueName);
        }

        return successCount;
    }
}