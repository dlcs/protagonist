using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Customers;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using API.Features.OriginStrategies.Credentials;
using DLCS.Core.Enum;
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

    public UpdateCustomerOriginStrategyHandler(DlcsContext dbContext, CredentialsExporter credentialsExporter)
    {
        this.dbContext = dbContext;
        this.credentialsExporter = credentialsExporter;
    }

    public async Task<ModifyEntityResult<CustomerOriginStrategy>> Handle(
        UpdateCustomerOriginStrategy request,
        CancellationToken cancellationToken)
    {
        var wipeCredentialsOnSuccess = false;
        
        var existingStrategy = await dbContext.CustomerOriginStrategies.SingleOrDefaultAsync(
            s => s.Id == request.StrategyId && s.Customer == request.CustomerId,
            cancellationToken);

        if (existingStrategy == null)
            return ModifyEntityResult<CustomerOriginStrategy>
                .Failure($"Couldn't find an origin strategy with the id {request.StrategyId}", WriteResult.NotFound);

        if (request.Regex.HasText())
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
            if(request.Strategy == OriginStrategyType.BasicHttp && !request.Credentials.HasText())
                return ModifyEntityResult<CustomerOriginStrategy>
                    .Failure("Credentials must be specified when using basic-http-authentication as an origin strategy", WriteResult.FailedValidation);
            
            // If the strategy was previously basic-http-authentication, wipe the credentials stored on S3
            if (existingStrategy.Strategy == OriginStrategyType.BasicHttp &&
                request.Strategy != OriginStrategyType.BasicHttp)
                wipeCredentialsOnSuccess = true;
            
            // If the strategy was previously s3-ambient, disable "optimised"
            if (existingStrategy.Strategy == OriginStrategyType.S3Ambient &&
                request.Strategy != OriginStrategyType.S3Ambient)
                existingStrategy.Optimised = false;
            
            existingStrategy.Strategy = request.Strategy.Value;
        }
        
        if (request.Optimised.HasValue)
        {
            if(request.Optimised.Value == true && existingStrategy.Strategy != OriginStrategyType.S3Ambient)
                return ModifyEntityResult<CustomerOriginStrategy>
                    .Failure("'Optimised' is only applicable when using s3-ambient as an origin strategy", WriteResult.FailedValidation);
            
            existingStrategy.Optimised = request.Optimised.Value;
        }
        
        if (request.Credentials.HasText())
        {
            if(!IsFullOriginStrategy(request))
                return ModifyEntityResult<CustomerOriginStrategy>
                    .Failure("A full origin strategy object is required when updating credentials",
                        WriteResult.FailedValidation);

            if (existingStrategy.Strategy == OriginStrategyType.BasicHttp)
            {
                
                var exportCredentialsResult = await credentialsExporter.ExportCredentials(
                    request.Credentials, existingStrategy.Customer, existingStrategy.Id, cancellationToken);
            
                if (exportCredentialsResult.IsError)
                    return ModifyEntityResult<CustomerOriginStrategy>.Failure(exportCredentialsResult.ErrorMessage!,
                        WriteResult.FailedValidation);
                
                existingStrategy.Credentials = exportCredentialsResult.S3Uri;
            }
            else
            {
                return ModifyEntityResult<CustomerOriginStrategy>
                    .Failure($"Credentials cannot be specified for strategy type '{existingStrategy.Strategy.GetDescription()}'",
                        WriteResult.FailedValidation);
            }
        }
        
        if (request.Order.HasValue)
            existingStrategy.Order = request.Order.Value;
        
        await dbContext.SaveChangesAsync(cancellationToken);

        if (wipeCredentialsOnSuccess)
        {
            await credentialsExporter.DeleteCredentials(existingStrategy);
            existingStrategy.Credentials = string.Empty;
        }
       
        return ModifyEntityResult<CustomerOriginStrategy>.Success(existingStrategy);
    }
    
    private bool IsFullOriginStrategy(UpdateCustomerOriginStrategy request)
        => (
            request.Regex.HasText() &&
            request.Credentials.HasText() &&
            request.Strategy.HasValue &&
            request.Optimised.HasValue &&
            request.Order.HasValue
            );
}
