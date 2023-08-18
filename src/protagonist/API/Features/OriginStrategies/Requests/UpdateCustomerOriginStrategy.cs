using API.Features.Image.Requests;
using API.Infrastructure.Requests;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core;
using DLCS.Core.Enum;
using DLCS.Model.Auth;
using DLCS.Model.Customers;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace API.Features.OriginStrategies.Requests;

public class UpdateCustomerOriginStrategy : IRequest<ModifyEntityResult<CustomerOriginStrategy>>
{
    public int CustomerId { get; }
    public string StrategyId { get; }
    public string? Regex { get; set; }
    public string? Credentials { get; set; }
    public OriginStrategyType? Strategy { get; set; }
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
    private readonly IBucketWriter bucketWriter;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly JsonSerializerOptions jsonSettings = new(JsonSerializerDefaults.Web);

    public UpdateCustomerOriginStrategyHandler(
        DlcsContext dbContext,
        ILogger<HostAssetAtOriginHandler> logger,
        IBucketWriter bucketWriter,
        IStorageKeyGenerator storageKeyGenerator)
    {
        this.dbContext = dbContext;
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
            return ModifyEntityResult<CustomerOriginStrategy>
                .Failure($"Couldn't find an origin strategy with the id {request.StrategyId}", WriteResult.NotFound);
        
        if (!string.IsNullOrWhiteSpace(request.Regex))
        {
            var regexUsed = await dbContext.CustomerOriginStrategies.AnyAsync(
                s => s.Customer == request.CustomerId && s.Regex == request.Regex && s.Id != existingStrategy.Id,
                cancellationToken);

            if (regexUsed)
                return ModifyEntityResult<CustomerOriginStrategy>.Failure(
                    "An origin strategy using the same regex already exists",
                    WriteResult.Conflict);

            existingStrategy.Regex = request.Regex;
        }
        
        if (request.Strategy.HasValue)
            existingStrategy.Strategy = request.Strategy.Value;
        
        if (request.Optimised.HasValue)
            existingStrategy.Optimised = request.Optimised.Value;
       
        if (request.Order.HasValue)
            existingStrategy.Order = request.Order.Value;
        
        if (existingStrategy.Strategy != OriginStrategyType.S3Ambient && existingStrategy.Optimised)
            return ModifyEntityResult<CustomerOriginStrategy>
                .Failure($"'Optimised' is only applicable when using s3-ambient as an origin strategy", WriteResult.FailedValidation);
        
        if(existingStrategy.Strategy == OriginStrategyType.BasicHttp && string.IsNullOrWhiteSpace(request.Credentials))
            return ModifyEntityResult<CustomerOriginStrategy>
                .Failure($"Credentials must be specified when using basic-http-authentication as an origin strategy", WriteResult.FailedValidation);
        
        if (!string.IsNullOrWhiteSpace(request.Credentials))
        {
            if (!(!string.IsNullOrEmpty(request.Regex) && request.Strategy.HasValue && request.Optimised.HasValue && request.Order.HasValue))
                return ModifyEntityResult<CustomerOriginStrategy>
                    .Failure($"A full origin strategy object is required when updating credentials",
                        WriteResult.FailedValidation);

            if (existingStrategy.Strategy != OriginStrategyType.BasicHttp)
                return ModifyEntityResult<CustomerOriginStrategy>
                    .Failure($"Credentials can only be specified when using basic-http-authentication as an origin strategy",
                        WriteResult.FailedValidation);
            try
            {
                var credentials = JsonSerializer.Deserialize<BasicCredentials>(request.Credentials, jsonSettings);
                
                if(string.IsNullOrWhiteSpace(credentials?.User))
                    return ModifyEntityResult<CustomerOriginStrategy>.Failure($"The credentials object requires an username", WriteResult.FailedValidation);
                if(string.IsNullOrWhiteSpace(credentials?.Password))
                    return ModifyEntityResult<CustomerOriginStrategy>.Failure($"The credentials object requires a password", WriteResult.FailedValidation);
               
                var credentialsJson = JsonSerializer.Serialize(credentials, jsonSettings);
                var objectInBucket = storageKeyGenerator.GetOriginStrategyCredentialsLocation(request.CustomerId, existingStrategy.Id);
                
                await bucketWriter.WriteToBucket(objectInBucket, credentialsJson, "application/json", cancellationToken);
                
                existingStrategy.Credentials = objectInBucket.GetS3Uri().ToString();
            }
            catch (Exception e)
            {
                return ModifyEntityResult<CustomerOriginStrategy>.Failure($"Invalid credentials JSON: {e.Message}", WriteResult.FailedValidation);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        existingStrategy.Credentials = "xxx";

        return ModifyEntityResult<CustomerOriginStrategy>.Success(existingStrategy);
    }
}

