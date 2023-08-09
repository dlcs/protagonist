using System.Text.Json;
using API.Features.Image.Requests;
using API.Infrastructure.Requests;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core;
using DLCS.Model.Auth;
using DLCS.Model.Customers;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Features.OriginStrategies.Requests;

public class CreateCustomerOriginStrategy : IRequest<ModifyEntityResult<CustomerOriginStrategy>>
{
    public int CustomerId { get; }
    
    public CustomerOriginStrategy Strategy { get; }
    
    public CreateCustomerOriginStrategy(int customerId, CustomerOriginStrategy strategy)
    {
        CustomerId = customerId;
        Strategy = strategy;
    }
}

public class CreateCustomerOriginStrategyHandler : IRequestHandler<CreateCustomerOriginStrategy, ModifyEntityResult<CustomerOriginStrategy>>
{  
    private readonly DlcsContext dbContext;
    private readonly ILogger<HostAssetAtOriginHandler> logger;
    private readonly IBucketWriter bucketWriter;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    
    public CreateCustomerOriginStrategyHandler(
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
    
    public async Task<ModifyEntityResult<CustomerOriginStrategy>> Handle(CreateCustomerOriginStrategy request, CancellationToken cancellationToken)
    {
        var existingStrategy = await dbContext.CustomerOriginStrategies.AsNoTracking().SingleOrDefaultAsync(
            s => s.Customer == request.CustomerId && s.Regex == request.Strategy.Regex, 
            cancellationToken);
        
        if (existingStrategy != null)
        {
            return ModifyEntityResult<CustomerOriginStrategy>.Failure("An origin strategy using the same regex already exists",
                WriteResult.Conflict);
        }

        var newStrategyId = Guid.NewGuid().ToString();
        var newStrategyCredentials = string.Empty;
        
        if (!string.IsNullOrWhiteSpace(request.Strategy.Credentials) && request.Strategy.Strategy == OriginStrategyType.BasicHttp)
        {
            try
            {
                JsonSerializer.Deserialize<BasicCredentials>(request.Strategy.Credentials);
            }
            catch (Exception e)
            {
                return ModifyEntityResult<CustomerOriginStrategy>.Failure(e.Message, WriteResult.Error);
            }
            
            var objectInBucket = storageKeyGenerator.GetOriginStrategyCredentialsLocation(request.CustomerId, newStrategyId);
            await bucketWriter.WriteToBucket(objectInBucket, request.Strategy.Credentials, "application/json", cancellationToken);
            newStrategyCredentials = objectInBucket.GetS3Uri().ToString();
        }
        
        var newStrategy = new CustomerOriginStrategy()
        {
            Id = newStrategyId,
            Customer = request.Strategy.Customer,
            Regex = request.Strategy.Regex,
            Strategy = request.Strategy.Strategy,
            Credentials = newStrategyCredentials,
            Optimised = request.Strategy.Optimised,
            Order = request.Strategy.Order
        };
        
        await dbContext.CustomerOriginStrategies.AddAsync(newStrategy, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        newStrategy.Credentials = "xxx";
        
        return ModifyEntityResult<CustomerOriginStrategy>.Success(newStrategy, WriteResult.Created);
    }
}