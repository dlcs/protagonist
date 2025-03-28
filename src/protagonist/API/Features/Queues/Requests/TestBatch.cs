using System.Collections;
using System.Collections.Generic;
using DLCS.AWS.SNS.Messaging;
using DLCS.Model.Assets;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Features.Queues.Requests;

/// <summary>
/// Tests batch to check if it has been superseded or is complete
/// </summary>
public class TestBatch : IRequest<bool?>
{
    public int CustomerId { get; }
    public int BatchId { get; }
    
    public TestBatch(int customerId, int batchId)
    {
        CustomerId = customerId;
        BatchId = batchId;
    }
}

public class TestBatchHandler : IRequestHandler<TestBatch, bool?>
{
    private readonly DlcsContext dlcsContext;
    private readonly ILogger<TestBatchHandler> logger;
    private readonly IBatchCompletedNotificationSender batchCompletedNotificationSender;

    public TestBatchHandler(
        DlcsContext dlcsContext,
        IBatchCompletedNotificationSender batchCompletedNotificationSender,
        ILogger<TestBatchHandler> logger)
    {
        this.dlcsContext = dlcsContext;
        this.batchCompletedNotificationSender = batchCompletedNotificationSender;
        this.logger = logger;
    }
    
    public async Task<bool?> Handle(TestBatch request, CancellationToken cancellationToken)
    {
        var batch = await GetBatchToTest(request, cancellationToken);

        if (batch == null)
        {
            return null;
        }

        var batchImages = await GetImagesForBatch(request, cancellationToken);

        bool changesMade = false;
        if (!batch.Superseded && IsBatchSuperseded(batchImages))
        {
            logger.LogDebug("Batch {BatchId} for superseded", request.BatchId);
            batch.Superseded = true;
            changesMade = true;
        }

        if (IsBatchComplete(batchImages))
        {
            logger.LogDebug("Batch {BatchId} complete", request.BatchId);
            if (batch.Finished == null)
            {
                logger.LogInformation("Batch {BatchId} complete but not finished. Setting Finished", request.BatchId);
                changesMade = true;
                batch.Finished = DateTime.UtcNow;
                await batchCompletedNotificationSender.SendBatchCompletedMessage(batch, cancellationToken);
            }

            if (batch.Count != batchImages.Count)
            {
                logger.LogInformation("Batch {BatchId} complete. Resetting counts", request.BatchId);
                changesMade = true;
                batch.Count = batchImages.Count;
                batch.Errors = batchImages.Count(i => !string.IsNullOrEmpty(i.Error));
                batch.Completed = batch.Count - batch.Errors;
            }
        }

        if (changesMade)
        {
            await dlcsContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        logger.LogTrace("Batch {BatchId} for Customer {Customer} tested but no changes made", request.BatchId,
            request.CustomerId);
        return false;
    }

    private static bool IsBatchComplete(IEnumerable<Asset> batchImages)
        => batchImages.All(i => i.Finished != null);

    private Task<Batch?> GetBatchToTest(TestBatch request, CancellationToken cancellationToken)
    {
        return dlcsContext.Batches
            .Where(b => b.Customer == request.CustomerId && b.Id == request.BatchId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private Task<List<Asset>> GetImagesForBatch(TestBatch request, CancellationToken cancellationToken)
    {
        return dlcsContext.Images.AsNoTracking()
            .Where(i => i.Customer == request.CustomerId && i.Batch == request.BatchId)
            .ToListAsync(cancellationToken);
    }
    
    private static bool IsBatchSuperseded(ICollection batchImages) => batchImages.Count == 0;
}
