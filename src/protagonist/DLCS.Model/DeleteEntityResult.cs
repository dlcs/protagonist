using System;
using DLCS.Core;

namespace DLCS.Model;

/// <summary>
/// Represents the result of a delete operation
/// </summary>
/// <typeparam name="T">Type of entity deleted</typeparam>
public class DeleteEntityResult<T>
    where T : class
{
    /// <summary>
    /// Deleted entity - only available if delete was successful
    /// </summary>
    public T? DeletedEntity { get; }
    
    /// <summary>
    /// The overall result of the attempted delete operation
    /// </summary>
    public DeleteResult Result { get; }

    public DeleteEntityResult(DeleteResult deleteResult, T? deletedEntity = default)
    {
        if (deleteResult == DeleteResult.Deleted && deletedEntity is null)
        {
            throw new InvalidOperationException(
                $"Deleted {typeof(T).Name} entity must be provided if status is 'Deleted'");
        }
        
        Result = deleteResult;
        DeletedEntity = deletedEntity;
    }
}