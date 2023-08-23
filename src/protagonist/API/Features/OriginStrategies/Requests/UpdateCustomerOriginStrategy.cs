using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Customers;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using API.Features.OriginStrategies.Credentials;
using DLCS.Core.Strings;

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
    private readonly CredentialsExporter credentialsExporter;

    public UpdateCustomerOriginStrategyHandler(
        DlcsContext dbContext,
        CredentialsExporter credentialsExporter)
    {
        this.dbContext = dbContext;
        this.credentialsExporter = credentialsExporter;
    }

    public async Task<ModifyEntityResult<CustomerOriginStrategy>> Handle(
        UpdateCustomerOriginStrategy request,
        CancellationToken cancellationToken)
    {
        var wipeCredentials = false;
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
        {
            if(existingStrategy.Strategy == OriginStrategyType.BasicHttp && request.Strategy != OriginStrategyType.BasicHttp
                && !string.IsNullOrWhiteSpace(existingStrategy.Credentials))
                wipeCredentials = true;
            existingStrategy.Strategy = request.Strategy.Value;
        }
        
        if (request.Optimised.HasValue)
            existingStrategy.Optimised = request.Optimised.Value;
        
        if (request.Order.HasValue)
            existingStrategy.Order = request.Order.Value;
        
        if(existingStrategy.Strategy == OriginStrategyType.BasicHttp && string.IsNullOrWhiteSpace(request.Credentials)
           && (request.Strategy.HasValue || request.Credentials.HasText()))
            return ModifyEntityResult<CustomerOriginStrategy>
                .Failure("Credentials must be specified when using basic-http-authentication as an origin strategy", WriteResult.FailedValidation);
        
        if (existingStrategy.Strategy != OriginStrategyType.S3Ambient && existingStrategy.Optimised
            && (request.Strategy.HasValue || request.Optimised.HasValue))
            return ModifyEntityResult<CustomerOriginStrategy>
                .Failure("'Optimised' is only applicable when using s3-ambient as an origin strategy", WriteResult.FailedValidation);
        
        if (!string.IsNullOrWhiteSpace(request.Credentials))
        {
            if (!(!string.IsNullOrEmpty(request.Regex) && request.Strategy.HasValue && request.Optimised.HasValue && request.Order.HasValue))
                return ModifyEntityResult<CustomerOriginStrategy>
                    .Failure("A full origin strategy object is required when updating credentials",
                        WriteResult.FailedValidation);

            if (existingStrategy.Strategy != OriginStrategyType.BasicHttp)
                return ModifyEntityResult<CustomerOriginStrategy>
                    .Failure("Credentials can only be specified when using basic-http-authentication as an origin strategy",
                        WriteResult.FailedValidation);

            var exportCredentialsResult =
                await credentialsExporter.ExportCredentials(request.Credentials, existingStrategy.Customer, existingStrategy.Id, cancellationToken);
            
            if (exportCredentialsResult.IsError)
                return ModifyEntityResult<CustomerOriginStrategy>.Failure(exportCredentialsResult.ErrorMessage!,
                    WriteResult.FailedValidation);

            existingStrategy.Credentials = exportCredentialsResult.S3Uri;
        }
        
        await dbContext.SaveChangesAsync(cancellationToken);
        
        if (wipeCredentials)
        {
            await credentialsExporter.DeleteCredentials(existingStrategy);
            existingStrategy.Credentials = string.Empty;
        }

        // Hide credentials in returned JSON object 
        existingStrategy.Credentials = "xxx";
        
        return ModifyEntityResult<CustomerOriginStrategy>.Success(existingStrategy);
    }
}
