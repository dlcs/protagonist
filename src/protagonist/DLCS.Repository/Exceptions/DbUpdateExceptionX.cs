using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DLCS.Repository.Exceptions;

/// <summary>
/// Extensions to <see cref="DbUpdateException"/> used to retrieve more 
/// database specific information about the thrown exception.
/// </summary>
/// <remarks>See https://haacked.com/archive/2022/12/12/specific-db-exception for more information.</remarks>
public static class DbUpdateExceptionX
{
    /// <summary>
    /// Retrieves a <see cref="DbError"/> with database specific error 
    /// information from the <see cref="DbUpdateException"/> thrown by EF Core. 
    /// </summary>
    /// <param name="exception">The <see cref="DbUpdateException"/> thrown.</param>
    /// <returns>A <see cref="DbError"/> or derived class if the inner 
    /// exception matches one of the supported types. Otherwise returns null.</returns>
    public static DbError? GetDatabaseError(this DbUpdateException exception)
    {
        if (exception.InnerException is PostgresException postgresException)
        {
            return postgresException.SqlState switch
            {
                PostgresErrorCodes.UniqueViolation => UniqueConstraintError
                    .FromPostgresException(postgresException),
                //... Other error codes mapped to other error types.
                _ => new DbError(
                    postgresException.TableName,
                    postgresException.ConstraintName,
                    postgresException)
            };
        }
        
        return null;
    }
}