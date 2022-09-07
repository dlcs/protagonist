using DLCS.Core;

namespace API.Infrastructure.Requests;

/// <summary>
/// Represents the result of a request to modify an entity
/// </summary>
/// <typeparam name="T"></typeparam>
public class AssetModifyResult<T>
    where T : class
{
    /// <summary>
    /// Enum representing overall result of operation
    /// </summary>
    public UpdateResult UpdateResult { get; private init;}
    
    /// <summary>
    /// Optional representation of entity
    /// </summary>
    public T? Entity { get; private init;}
    
    /// <summary>
    /// Optional error message if didn't succeed
    /// </summary>
    public string? Error { get; private init; }

    public static AssetModifyResult<T> Failure(string error, UpdateResult result = UpdateResult.Unknown)
        => new() { Error = error, UpdateResult = result };

    public static AssetModifyResult<T> Success(T entity, UpdateResult result = UpdateResult.Updated)
        => new() { Entity = entity, UpdateResult = result };
}