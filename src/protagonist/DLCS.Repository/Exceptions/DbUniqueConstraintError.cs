using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DLCS.Repository.Exceptions;

/// <summary>
/// Provides additional Postgres specific information about a 
/// <see cref="DbUpdateException"/> thrown by EF Core.This describes 
/// the case where the exception is a unique constraint violation.
/// </summary>
/// <param name="ColumnNames">The column names parsed from the constraint 
/// name assuming the constraint follows the "IX_{Table}_{Column1}_..._{ColumnN}" naming convention.</param>
/// <param name="TableName">The table involved, if any.</param>
/// <param name="ConstraintName">The constraint involved, if any.</param>
/// <param name="Exception">The unwrapped database provider specific exception.</param>
/// <remarks>See https://haacked.com/archive/2022/12/12/specific-db-exception for more information.</remarks>
public record UniqueConstraintError(
    IReadOnlyList<string> ColumnNames,
    string? TableName,
    string? ConstraintName,
    Exception Exception) : DbError(TableName, ConstraintName, Exception) {
    
    /// <summary>
    /// Creates a <see cref="UniqueConstraintError"/> from a <see cref="PostgresException"/>.
    /// </summary>
    /// <param name="postgresException">The <see cref="PostgresException"/>.</param>
    /// <returns>A <see cref="UniqueConstraintError"/> with extra information about the unique constraint violation.</returns>
    public static UniqueConstraintError FromPostgresException(PostgresException postgresException)
    {
        var constraintName = postgresException.ConstraintName;
        var tableName = postgresException.TableName;
       
        var constrainPrefix = tableName != null ? $"IX_{tableName}_" : null;

        var columnNames = Array.Empty<string>();
        
        if (constrainPrefix != null
            && constraintName != null
            && constraintName.StartsWith(constrainPrefix, StringComparison.Ordinal))
        {
            columnNames = constraintName[constrainPrefix.Length..].Split('_');
        }
        
        return new UniqueConstraintError(columnNames, tableName, constraintName, postgresException);
    }
}