using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core;
using DLCS.Model.Customers;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Features.OriginStrategies.Requests;

public class DeleteCustomerOriginStrategy : IRequest<ResultMessage<DeleteResult>>
{
    public int CustomerId { get; }
    public string StrategyId { get; }

    public DeleteCustomerOriginStrategy(int customerId, string strategyId)
    {
        CustomerId = customerId;
        StrategyId = strategyId;
    }
    
    public class DeleteCustomerOriginStrategyHandler : IRequestHandler<DeleteCustomerOriginStrategy, ResultMessage<DeleteResult>>
    {
        private readonly DlcsContext dbContext;
        private readonly IBucketWriter bucketWriter;
        private readonly ILogger<DeleteCustomerOriginStrategyHandler> logger;

        public DeleteCustomerOriginStrategyHandler(DlcsContext dbContext, IBucketWriter bucketWriter,
            ILogger<DeleteCustomerOriginStrategyHandler> logger)
        {
            this.dbContext = dbContext;
            this.bucketWriter = bucketWriter;
            this.logger = logger;
        }

        public async Task<ResultMessage<DeleteResult>> Handle(DeleteCustomerOriginStrategy request,
            CancellationToken cancellationToken)
        {
            var strategy = await dbContext.CustomerOriginStrategies.SingleOrDefaultAsync(
                s => s.Customer == request.CustomerId &&
                      s.Id == request.StrategyId,
                cancellationToken: cancellationToken);

            if (strategy == null) 
                return new ResultMessage<DeleteResult>(
                    $"Deletion failed - origin strategy {request.StrategyId} was not found", DeleteResult.NotFound);

            dbContext.CustomerOriginStrategies.Remove(strategy);
            
            await dbContext.SaveChangesAsync(cancellationToken);

            await TryDeleteCredentials(strategy);
            
            return new ResultMessage<DeleteResult>(
                $"Origin strategy {request.StrategyId} successfully deleted", DeleteResult.Deleted);
        }

        private async Task TryDeleteCredentials(CustomerOriginStrategy strategy)
        {
            if (strategy.Credentials.StartsWith("s3://"))
            {
                var objectInBucket = RegionalisedObjectInBucket.Parse(strategy.Credentials);
                if (objectInBucket != null)
                {
                    await bucketWriter.DeleteFromBucket(objectInBucket);
                }
                else
                {
                    logger.LogInformation("Unable to parse S3 URI {S3Uri} to ObjectInBucket", strategy.Credentials);
                }
            }
        }
    }
}