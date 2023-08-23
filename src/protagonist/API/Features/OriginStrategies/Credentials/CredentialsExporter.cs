using System.Text.Json;
using API.Features.OriginStrategies.Requests;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Model.Auth;
using DLCS.Model.Customers;
using Microsoft.Extensions.Logging;

namespace API.Features.OriginStrategies.Credentials;

/// <summary>
/// Class that encapsulates logic for updating and deleting customer origin strategy credentials on S3
/// </summary>
public class CredentialsExporter
{
    private readonly IBucketWriter bucketWriter;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly ILogger<CredentialsExporter> logger;
    private static readonly JsonSerializerOptions JsonSettings = new(JsonSerializerDefaults.Web);

    public CredentialsExporter(IBucketWriter bucketWriter, IStorageKeyGenerator storageKeyGenerator, ILogger<CredentialsExporter> logger)
    {
        this.bucketWriter = bucketWriter;
        this.storageKeyGenerator = storageKeyGenerator;
        this.logger = logger;
    }
    
    public async Task<ExportCredentialsResult> ExportCredentials(string jsonCredentials, int customerId, string strategyId, CancellationToken cancellationToken)
    {
        try
        {
            var credentials = JsonSerializer.Deserialize<BasicCredentials>(jsonCredentials, JsonSettings);

            if (string.IsNullOrWhiteSpace(credentials?.User))
                return ExportCredentialsResult.Error("The credentials object requires an username");
            if (string.IsNullOrWhiteSpace(credentials?.Password))
                return ExportCredentialsResult.Error("The credentials object requires a password");

            var credentialsJson = JsonSerializer.Serialize(credentials, JsonSettings);
            var objectInBucket =
                storageKeyGenerator.GetOriginStrategyCredentialsLocation(customerId, strategyId);

            await bucketWriter.WriteToBucket(objectInBucket, credentialsJson, "application/json", cancellationToken);

            return ExportCredentialsResult.Success(objectInBucket.GetS3Uri().ToString());
        }
        catch (Exception ex)
        {
            logger.LogInformation("Unable to export credentials to S3: {exceptionMessage}", ex.Message);
            return ExportCredentialsResult.Error("Invalid credentials JSON");
        }
    }
    
    public async Task TryDeleteCredentials(CustomerOriginStrategy strategy)
    {
        if (!strategy.Credentials.StartsWith("s3://")) 
            return;
        
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

public class ExportCredentialsResult
{
    public string? S3Uri { get; private init; }
    public string? ErrorMessage { get; private init; }
    public bool IsError { get; private init; }
        
    public static ExportCredentialsResult Success(string s3Uri) => new() { S3Uri = s3Uri };
    public static ExportCredentialsResult Error(string errorMessage) => new() { ErrorMessage = errorMessage, IsError = true };
}