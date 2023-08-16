using System.Text.Json;
using API.Infrastructure.Requests;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core;
using DLCS.Model.Auth;
using DLCS.Model.Customers;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
    private readonly IBucketWriter bucketWriter;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly JsonSerializerOptions jsonSettings = new(JsonSerializerDefaults.Web);
    
    public CreateCustomerOriginStrategyHandler(
        DlcsContext dbContext,
        IBucketWriter bucketWriter,
        IStorageKeyGenerator storageKeyGenerator)
    {
        this.dbContext = dbContext;
        this.bucketWriter = bucketWriter;
        this.storageKeyGenerator = storageKeyGenerator;
    }
    
    public async Task<ModifyEntityResult<CustomerOriginStrategy>> Handle(CreateCustomerOriginStrategy request, CancellationToken cancellationToken)
    {
        var regexUsed = await dbContext.CustomerOriginStrategies.AsNoTracking().AnyAsync(
            s => s.Customer == request.CustomerId && s.Regex == request.Strategy.Regex, 
            cancellationToken);
        
        if (regexUsed)
            return ModifyEntityResult<CustomerOriginStrategy>.Failure("An origin strategy using the same regex already exists",
                WriteResult.Conflict);

        var newStrategyId = Guid.NewGuid().ToString();
        var newStrategyCredentials = string.Empty;
        
        if (request.Strategy.Strategy == OriginStrategyType.BasicHttp)
        {
            try
            {
                var credentials = JsonSerializer.Deserialize<BasicCredentials>(request.Strategy.Credentials, jsonSettings);
                
                if(string.IsNullOrWhiteSpace(credentials?.User))
                    return ModifyEntityResult<CustomerOriginStrategy>.Failure($"The credentials object requires an username", WriteResult.FailedValidation);
                if(string.IsNullOrWhiteSpace(credentials?.Password))
                    return ModifyEntityResult<CustomerOriginStrategy>.Failure($"The credentials object requires a password", WriteResult.FailedValidation);
               
                var credentialsJson = JsonSerializer.Serialize(credentials, jsonSettings);
                var objectInBucket = storageKeyGenerator.GetOriginStrategyCredentialsLocation(request.CustomerId, newStrategyId);
                
                await bucketWriter.WriteToBucket(objectInBucket, credentialsJson, "application/json", cancellationToken);
                
                newStrategyCredentials = objectInBucket.GetS3Uri().ToString();
            }
            catch (Exception e)
            {
                return ModifyEntityResult<CustomerOriginStrategy>.Failure($"Invalid credentials JSON: {e.Message}", WriteResult.FailedValidation);
            }
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