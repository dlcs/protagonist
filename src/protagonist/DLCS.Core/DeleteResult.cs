namespace DLCS.Core;

/// <summary>
/// Represents the result of a delete operation
/// </summary>
public enum DeleteResult
{
    /// <summary>
    /// Item not deleted as it could not be found
    /// </summary>
    NotFound,
    
    /// <summary>
    /// Item was successfully deleted
    /// </summary>
    Deleted,
    
    /// <summary>
    /// There is a user addressable error while deleting
    /// </summary>
    Conflict,

    /// <summary>
    /// There was an error deleting
    /// </summary>
    Error
}