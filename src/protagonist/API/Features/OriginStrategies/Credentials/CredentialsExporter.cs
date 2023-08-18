using System.Text.Json;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Model.Auth;
using DLCS.Model.Customers;

namespace API.Features.OriginStrategies.Credentials;

public class CredentialsExporter
{
    private readonly IBucketWriter bucketWriter;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private static readonly JsonSerializerOptions JsonSettings = new(JsonSerializerDefaults.Web);

    public CredentialsExporter(IBucketWriter bucketWriter, IStorageKeyGenerator storageKeyGenerator)
    {
        this.bucketWriter = bucketWriter;
        this.storageKeyGenerator = storageKeyGenerator;
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
        catch (Exception)
        {
            return ExportCredentialsResult.Error("Invalid credentials JSON");
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