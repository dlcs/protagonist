using API.Features.Image.Requests;
using API.Infrastructure.Requests;
using DLCS.AWS.S3;
using DLCS.Core;
using DLCS.Core.Enum;
using DLCS.Model.Customers;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Features.OriginStrategies.Requests;

public class UpdateCustomerOriginStrategy : IRequest<ModifyEntityResult<CustomerOriginStrategy>>
{
    public int CustomerId { get; }
    public string StrategyId { get; }
    public string? Regex { get; set; }
    public string? Strategy { get; set; }
    public int? Order { get; set; }
    public bool? Optimised { get; set; }
    
    public UpdateCustomerOriginStrategy(int customerId, string strategyId)
    {
        CustomerId = customerId;
        StrategyId = strategyId;
    }
}

public class UpdateCustomerOriginStrategyHandler : IRequestHandler<UpdateCustomerOriginStrategy, ModifyEntityResult<CustomerOriginStrategy>>
{  
    private readonly DlcsContext dbContext;
    private readonly ILogger<HostAssetAtOriginHandler> logger;
    private readonly IBucketWriter bucketWriter;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    
    public UpdateCustomerOriginStrategyHandler(
        DlcsContext dbContext,
        ILogger<HostAssetAtOriginHandler> logger,
        IBucketWriter bucketWriter,
        IStorageKeyGenerator storageKeyGenerator)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.bucketWriter = bucketWriter;
        this.storageKeyGenerator = storageKeyGenerator;
    }
    
    public async Task<ModifyEntityResult<CustomerOriginStrategy>> Handle(
        UpdateCustomerOriginStrategy request, 
        CancellationToken cancellationToken)
    {
        var existingStrategy = await dbContext.CustomerOriginStrategies.SingleOrDefaultAsync(
            s => s.Id == request.StrategyId && s.Customer == request.CustomerId, 
            cancellationToken);
        
        if (existingStrategy == null)
        {
            return ModifyEntityResult<CustomerOriginStrategy>
                .Failure($"Couldn't find an origin strategy with the id {request.StrategyId}", WriteResult.NotFound);
        }
        
        if (!string.IsNullOrWhiteSpace(request.Regex))
            existingStrategy.Regex = request.Regex;
        if (!string.IsNullOrWhiteSpace(request.Strategy))
            existingStrategy.Strategy = request.Strategy.GetEnumFromString<OriginStrategyType>();
        if (request.Optimised.HasValue)
            existingStrategy.Optimised = request.Optimised.Value;
        if (request.Order.HasValue)
            existingStrategy.Order = request.Order.Value;
        
        if (existingStrategy.Strategy != OriginStrategyType.S3Ambient && existingStrategy.Optimised)
            return ModifyEntityResult<CustomerOriginStrategy>
                .Failure($"'Optimised' is only applicable if the origin strategy is s3-ambient", WriteResult.NotFound);
        
        await dbContext.SaveChangesAsync(cancellationToken);

        existingStrategy.Credentials = "xxx";
        
        return ModifyEntityResult<CustomerOriginStrategy>.Success(existingStrategy);
    }
}

