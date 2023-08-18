using System.Text.Json;
using API.Features.OriginStrategies.Credentials;
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
    private CredentialsExporter credentialsExporter;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly JsonSerializerOptions jsonSettings = new(JsonSerializerDefaults.Web);
    
    public CreateCustomerOriginStrategyHandler(
        DlcsContext dbContext,
        IBucketWriter bucketWriter,
        IStorageKeyGenerator storageKeyGenerator, CredentialsExporter credentialsExporter)
    {
        this.dbContext = dbContext;
        this.bucketWriter = bucketWriter;
        this.storageKeyGenerator = storageKeyGenerator;
        this.credentialsExporter = credentialsExporter;
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
            var exportCredentialsResult =
                await credentialsExporter.ExportCredentials(request.Strategy.Credentials, request.CustomerId, newStrategyId, cancellationToken);
            if (exportCredentialsResult.IsError)
                return ModifyEntityResult<CustomerOriginStrategy>.Failure(exportCredentialsResult.ErrorMessage!,
                    WriteResult.FailedValidation);

            newStrategyCredentials = exportCredentialsResult.S3Uri;
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