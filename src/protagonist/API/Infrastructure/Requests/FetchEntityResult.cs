namespace API.Infrastructure.Requests;

/// <summary>
/// Represents the result of a request to load an entity
/// </summary>
/// <typeparam name="T">Type of entity being modified</typeparam>
public class FetchEntityResult<T>
    where T : class
{
    /// <summary>
    /// Optional representation of entity
    /// </summary>
    public T? Entity { get; private init;}
    
    /// <summary>
    /// Optional error message if didn't succeed
    /// </summary>
    public string? ErrorMessage { get; private init; }
    
    /// <summary>
    /// If true an error occured fetching resource
    /// </summary>
    public bool Error { get; private init; }
    
    /// <summary>
    /// If true an error occured resources count not be found
    /// </summary>
    public bool EntityNotFound { get; private init; }

    public static FetchEntityResult<T> Failure(string? errorMessage)
        => new() { ErrorMessage = errorMessage, Error = true };
    
    public static FetchEntityResult<T> NotFound(string? errorMessage = null)
        => new() { ErrorMessage = errorMessage, EntityNotFound = true };

    public static FetchEntityResult<T> Success(T entity)
        => new() { Entity = entity };
}