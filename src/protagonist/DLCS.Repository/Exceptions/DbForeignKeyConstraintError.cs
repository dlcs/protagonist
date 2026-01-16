using System;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DLCS.Repository.Exceptions;

/// <summary>
/// Provides additional Postgres specific information about a  <see cref="DbUpdateException"/> thrown by EF Core.
/// This describes the case where the exception is a foreign key constraint violation.
/// </summary>
/// <param name="TableName">The main table involved, if any.</param>
/// <param name="SecondaryTableName">The secondary table involved, if any.</param>
/// <param name="ConstraintName">The constraint involved, if any.</param>
/// <param name="Exception">The unwrapped database provider specific exception.</param>
/// <remarks>Parsing is done assuming naming convention "FK_{Table1}_{Table2}_{Column1}". Note that column parsing not
/// done as longer column names are truncated with '~'. e.g.
/// FK_DefaultDeliveryChannels_DeliveryChannelPolicies_DeliveryCha~
/// FK_ImageDeliveryChannels_DeliveryChannelPolicies_DeliveryChann~
/// </remarks>
public record DbForeignKeyConstraintError(
    string? TableName,
    string? SecondaryTableName,
    string? ConstraintName,
    Exception Exception) : DbError(TableName, ConstraintName, Exception)
{
    /// <summary>
    /// Creates a <see cref="DbForeignKeyConstraintError"/> from a <see cref="PostgresException"/>.
    /// </summary>
    /// <param name="postgresException">The <see cref="PostgresException"/>.</param>
    /// <returns>A <see cref="DbForeignKeyConstraintError"/> with extra information about the foreign key violation.</returns>
    public static DbForeignKeyConstraintError FromPostgresException(PostgresException postgresException)
    {
        var constraintName = postgresException.ConstraintName;
        var tableName = postgresException.TableName;
       
        var constraintPrefix = tableName != null ? $"FK_{tableName}_" : null;

        string? secondaryTableName = null;
        
        if (constraintPrefix != null
            && constraintName != null
            && constraintName.StartsWith(constraintPrefix, StringComparison.Ordinal))
        {
            secondaryTableName = constraintName[constraintPrefix.Length..].Split('_')[0];
        }


        return new DbForeignKeyConstraintError(postgresException.TableName, secondaryTableName, postgresException.ConstraintName,
            postgresException);
    }
}
