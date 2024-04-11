using DLCS.Core;

namespace API.Infrastructure.Requests;

/// <summary>
/// Represents the result of a request to modify an entity
/// </summary>
/// <typeparam name="T">Type of entity being modified</typeparam>
public class ModifyEntityResult<T> : IModifyRequest
    where T : class
{
    /// <summary>
    /// Enum representing overall result of operation
    /// </summary>
    public WriteResult WriteResult { get; private init;}
    
    /// <summary>
    /// Optional representation of entity
    /// </summary>
    public T? Entity { get; private init;}
    
    /// <summary>
    /// Optional error message if didn't succeed
    /// </summary>
    public string? Error { get; private init; }
    
    /// <summary>
    /// Explicit value stating success or failure
    /// </summary>
    public bool IsSuccess { get; private init; }

    public static ModifyEntityResult<T> Failure(string error, WriteResult result = WriteResult.Unknown)
        => new() { Error = error, WriteResult = result, IsSuccess = false };

    public static ModifyEntityResult<T> Success(T entity, WriteResult result = WriteResult.Updated)
        => new() { Entity = entity, WriteResult = result, IsSuccess = true };
}

/// <summary>
/// Represents the result of a request to modify an entity
/// </summary>
public class DeleteEntityResult : IModifyRequest
{
    /// <summary>
    /// The associated value.
    /// </summary>
    public DeleteResult Value { get; private init; }
    
    /// <summary>
    /// The message related to the result
    /// </summary>
    public string? Message { get; private init; }
    
    public bool IsSuccess => Value == DeleteResult.Deleted;
    
    public static DeleteEntityResult Success => new() { Value = DeleteResult.Deleted };

    public static DeleteEntityResult Failure(string message, DeleteResult result) =>
        new() { Message = message, Value = result };
}

/// <summary>
/// Marker interface for operations that alter an underlying entity
/// </summary>
public interface IModifyRequest
{
    /// <summary>
    /// Whether record is deemed as success or not
    /// </summary>
    bool IsSuccess { get; }
}