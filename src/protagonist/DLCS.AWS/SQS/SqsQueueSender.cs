using Amazon.SQS;
using Amazon.SQS.Model;
using DLCS.Core;
using Microsoft.Extensions.Logging;

namespace DLCS.AWS.SQS;

/// <summary>
/// Implementation of <see cref="IQueueSender"/> using Sqs for backing queue
/// </summary>
public class SqsQueueSender(IAmazonSQS client, SqsQueueUtilities queueUtilities, ILogger<SqsQueueSender> logger)
    : IQueueSender
{
    public async Task<bool> QueueMessage(string queueName, string messageContents,
        IDictionary<string, string>? messageAttributes, CancellationToken cancellationToken = default)
    {
        var queueUrl = await QueueLookup.GetQueueUrl(queueUtilities, queueName, cancellationToken);
        try
        {
            var message = new SendMessageRequest(queueUrl, messageContents)
            {
                MessageAttributes = GetMessageAttributesDictionary(messageAttributes)
            };

            var result = await client.SendMessageAsync(message, cancellationToken);
            return result.HttpStatusCode.IsSuccess();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending message to {QueueName}", queueName);
            return false;
        }
    }

    /// <summary>
    /// Converts CLR string-string dictionary to one with Amazon SQS specific <see cref="MessageAttributeValue"/>
    /// </summary>
    /// <param name="messageAttributes">attributes as string-string dictionary or null if no custom message attributes are needed</param>
    /// <returns>New instance of a string-<see cref="MessageAttributeValue"/> dictionary</returns>
    private static Dictionary<string, MessageAttributeValue>? GetMessageAttributesDictionary(
        IDictionary<string, string>? messageAttributes)
        => messageAttributes?.ToDictionary(kvp => kvp.Key,
            kvp => new MessageAttributeValue { StringValue = kvp.Value, DataType = "String" });

    public async Task<int> QueueMessages(string queueName, IReadOnlyCollection<string> messageContents,
        string batchIdentifier, IDictionary<string, string>? messageAttributes,
        CancellationToken cancellationToken = default)
    {
        const int batchSize = 10;
        var queueUrl = await QueueLookup.GetQueueUrl(queueUtilities, queueName, cancellationToken);
        var successCount = 0;
        var batchCount = 0;
        var count = 0;

        foreach (var batch in messageContents.Chunk(batchSize))
        {
            var batchPrefix = $"{batchIdentifier}_{++batchCount}";
            var entries = batch
                .Select(c => new SendMessageBatchRequestEntry($"{batchPrefix}_{++count}", c)
                {
                    // Note: It seems recommended to have an instance-per-entry, not reuse the same one
                    MessageAttributes = GetMessageAttributesDictionary(messageAttributes)
                })
                .ToList();

            // Each chunk is an independent SQS call - a failure sending one must not abandon those that follow it,
            // else the caller is told nothing was sent while earlier chunks are already on the queue
            try
            {
                var batchResult = await client.SendMessageBatchAsync(queueUrl, entries, cancellationToken);

                if (!batchResult.HttpStatusCode.IsSuccess())
                {
                    logger.LogError("Overall batch failure for {BatchPrefix}. StatusCode: {StatusCode}", batchPrefix,
                        batchResult.HttpStatusCode);
                }

                foreach (var errorEntry in batchResult.Failed ?? [])
                {
                    logger.LogError("Failed message {MessageId}, message: {Error}", errorEntry.Id,
                        errorEntry.Message);
                }

                successCount += batchResult.Successful?.Count ?? 0;
            }
            catch (BatchRequestTooLongException)
            {
                logger.LogError("Batch {BatchIdentifier} chunk {BatchPrefix} too long. Batch size: {BatchSize}",
                    batchIdentifier, batchPrefix, batchSize);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending chunk {BatchPrefix} to {QueueName}", batchPrefix, queueName);
            }
        }

        if (successCount != messageContents.Count)
        {
            logger.LogError(
                "Batch {BatchIdentifier} to {QueueName} incomplete - queued {SuccessCount} of {MessageCount} messages",
                batchIdentifier, queueName, successCount, messageContents.Count);
        }

        return successCount;
    }
}
