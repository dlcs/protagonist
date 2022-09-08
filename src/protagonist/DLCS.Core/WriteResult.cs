namespace DLCS.Core;

/// <summary>
/// Represents the result of a Create or Update operation
/// </summary>
public enum WriteResult
{
    /// <summary>
    /// Default state - likely operation has yet to be run.
    /// </summary>
    Unknown,
    
    /// <summary>
    /// Source item not found
    /// </summary>
    NotFound,
    
    /// <summary>
    /// An error occurred handling update
    /// </summary>
    Error,
    
    /// <summary>
    /// The update values would have resulted in a conflict with an existing resource
    /// </summary>
    Conflict,
    
    /// <summary>
    /// Request failed validation
    /// </summary>
    FailedValidation,
    
    /// <summary>
    /// Entity was successfully updated
    /// </summary>
    Updated,
    
    /// <summary>
    /// Entity was successfully created
    /// </summary>
    Created,
    
    /// <summary>
    /// Predefined storage limits exceeded
    /// </summary>
    StorageLimitExceeded,
}