using API.Features.OriginStrategies.Credentials;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Core.Enum;
using DLCS.Core.Strings;
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

public class UpdateCustomerOriginStrategyHandler(
    DlcsContext dbContext,
    CredentialsExporter credentialsExporter,
    ILogger<UpdateCustomerOriginStrategyHandler> logger)
    : IRequestHandler<UpdateCustomerOriginStrategy, ModifyEntityResult<CustomerOriginStrategy>>
{
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

        if (request.Regex.HasText())
        {
            var regexUsed = await dbContext.CustomerOriginStrategies.AnyAsync(
                s => s.Customer == request.CustomerId && s.Regex == request.Regex && s.Id != existingStrategy.Id,
                cancellationToken);

            if (regexUsed)
            {
                return ModifyEntityResult<CustomerOriginStrategy>.Failure(
                    "An origin strategy using the same regex already exists",
                    WriteResult.Conflict);
            }

            existingStrategy.Regex = request.Regex;
        }

        var wipeCredentialsOnSuccess = false;
        if (request.Strategy.HasValue)
        {
            if (request.Strategy is OriginStrategyType.BasicHttp or OriginStrategyType.SFTP &&
                !request.Credentials.HasText())
            {
                return ModifyEntityResult<CustomerOriginStrategy>.Failure(
                    $"Credentials must be specified when using {request.Strategy.Value.GetDescription()} as an origin strategy",
                    WriteResult.FailedValidation);
            }

            // If the strategy was previously basic-http-authentication OR sftp and no longer either, delete credentials
            wipeCredentialsOnSuccess = ShouldClearCredentials(existingStrategy.Strategy, request.Strategy.Value);

            // If the strategy was previously s3-ambient, disable "optimised"
            if (existingStrategy.Strategy == OriginStrategyType.S3Ambient &&
                request.Strategy != OriginStrategyType.S3Ambient)
            {
                existingStrategy.Optimised = false;
            }

            existingStrategy.Strategy = request.Strategy.Value;
        }

        if (request.Optimised.HasValue)
        {
            if (request.Optimised.Value && existingStrategy.Strategy != OriginStrategyType.S3Ambient)
            {
                return ModifyEntityResult<CustomerOriginStrategy>
                    .Failure("'Optimised' is only applicable when using s3-ambient as an origin strategy",
                        WriteResult.FailedValidation);
            }

            existingStrategy.Optimised = request.Optimised.Value;
        }

        if (request.Credentials.HasText())
        {
            if (!IsFullOriginStrategy(request))
            {
                return ModifyEntityResult<CustomerOriginStrategy>
                    .Failure("A full origin strategy object is required when updating credentials",
                        WriteResult.FailedValidation);
            }

            if (existingStrategy.Strategy is OriginStrategyType.BasicHttp or OriginStrategyType.SFTP)
            {

                var exportCredentialsResult = await credentialsExporter.ExportCredentials(
                    request.Credentials, existingStrategy.Customer, existingStrategy.Id, cancellationToken);

                if (exportCredentialsResult.IsError)
                {
                    return ModifyEntityResult<CustomerOriginStrategy>.Failure(exportCredentialsResult.ErrorMessage!,
                        WriteResult.FailedValidation);
                }

                existingStrategy.Credentials = exportCredentialsResult.S3Uri;
            }
            else
            {
                return ModifyEntityResult<CustomerOriginStrategy>
                    .Failure(
                        $"Credentials cannot be specified for strategy type '{existingStrategy.Strategy.GetDescription()}'",
                        WriteResult.FailedValidation);
            }
        }

        if (request.Order.HasValue) existingStrategy.Order = request.Order.Value;

        await dbContext.SaveChangesAsync(cancellationToken);

        if (wipeCredentialsOnSuccess)
        {
            logger.LogInformation("Deleting credentials for COS {StrategyId}", existingStrategy.Id);
            await credentialsExporter.DeleteCredentials(existingStrategy);
            existingStrategy.Credentials = string.Empty;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ModifyEntityResult<CustomerOriginStrategy>.Success(existingStrategy);
    }

    /// <summary>
    /// If the origin strategy previously stored credentials but no longer does, delete them 
    /// </summary>
    private static bool ShouldClearCredentials(OriginStrategyType existingStrategy, OriginStrategyType newStrategy)
        => existingStrategy is OriginStrategyType.BasicHttp or OriginStrategyType.SFTP
           && newStrategy is not (OriginStrategyType.BasicHttp or OriginStrategyType.SFTP);

    private static bool IsFullOriginStrategy(UpdateCustomerOriginStrategy request)
        => request.Regex.HasText() &&
           request.Credentials.HasText() &&
           request.Strategy.HasValue &&
           request.Optimised.HasValue &&
           request.Order.HasValue;
}
