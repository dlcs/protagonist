using API.Features.OriginStrategies.Credentials;
using DLCS.Core;
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
        private readonly ILogger<DeleteCustomerOriginStrategyHandler> logger;
        private readonly CredentialsExporter credentialsExporter;
        
        public DeleteCustomerOriginStrategyHandler(DlcsContext dbContext, ILogger<DeleteCustomerOriginStrategyHandler> logger, CredentialsExporter credentialsExporter)
        {
            this.dbContext = dbContext;
            this.logger = logger;
            this.credentialsExporter = credentialsExporter;
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

            await credentialsExporter.DeleteCredentials(strategy);
            
            return new ResultMessage<DeleteResult>(
                $"Origin strategy {request.StrategyId} successfully deleted", DeleteResult.Deleted);
        }
    }
}