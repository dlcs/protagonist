namespace API.Features.Customer.Requests;

public class CreateApiKeyResult
{
    public bool CreateSuccess { get; private init; }
    public string? Key { get; private init; }
    public string? Secret { get; private init; }
    public string? Error { get; private init; }

    public static CreateApiKeyResult Fail(string error) => new() { Error = error };
    public static CreateApiKeyResult Success(string key, string secret) 
        => new() { Key = key, Secret = secret, CreateSuccess = true };
}