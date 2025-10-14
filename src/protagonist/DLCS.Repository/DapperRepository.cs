using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DLCS.Repository;

/// <summary>
/// Marker interface for repositories that user Dapper.
/// Used as a hook for handle extension methods. Do not implement this alone, use <see cref="IDapperConfigRepository"/>
/// or <see cref="IDapperContextRepository"/>
/// </summary>
public interface IDapperRepository
{
}

/// <summary>
/// Interface for Dapper repositories that get DbConnection string from DlcsContext
/// </summary>
public interface IDapperContextRepository : IDapperRepository
{
    DlcsContext DlcsContext { get; }
}

/// <summary>
/// Interface for Dapper repositories that get DbConnection string from configuration
/// </summary>
public interface IDapperConfigRepository : IDapperRepository
{
    IConfiguration Configuration { get; }
}

public static class DapperRepositoryX
{
    public static async Task<T?> QueryFirstOrDefaultAsync<T>(this IDapperRepository repository, string sql,
        object? param = null)
    {
        return await HandleCommand<T?>(repository,
            dbConnection => dbConnection.QueryFirstOrDefaultAsync<T?>(sql, param));
    }

    public static async Task<dynamic?> QueryFirstOrDefaultAsync(this IDapperRepository repository, string sql,
        object? param = null)
    {
        return await HandleCommand<dynamic?>(repository,
            dbConnection => dbConnection.QuerySingleOrDefaultAsync(sql, param));
    }

    public static async Task<T?> QuerySingleOrDefaultAsync<T>(this IDapperRepository repository, string sql,
        object? param = null)
    {
        return await HandleCommand<T>(repository,
            dbConnection => dbConnection.QueryFirstOrDefaultAsync<T>(sql, param));
    }

    public static async Task<dynamic?> QuerySingleOrDefaultAsync(this IDapperRepository repository, string sql,
        object? param = null)
    {
        return await HandleCommand<dynamic?>(repository,
            dbConnection => dbConnection.QuerySingleOrDefaultAsync(sql, param));
    }

    public static async Task<T> QuerySingleAsync<T>(this IDapperRepository repository, string sql, object? param = null)
    {
        return await HandleCommand<T>(repository,
            dbConnection => dbConnection.QuerySingleAsync<T>(sql, param));
    }

    public static async Task<IEnumerable<T>> QueryAsync<T>(this IDapperRepository repository, string sql,
        object? param = null)
    {
        return await HandleCommand<IEnumerable<T>>(repository,
            dbConnection => dbConnection.QueryAsync<T>(sql, param));
    }

    public static async Task<IEnumerable<dynamic>> QueryAsync(this IDapperRepository repository, string sql,
        object? param = null)
    {
        return await HandleCommand<IEnumerable<dynamic>>(repository,
            dbConnection => dbConnection.QueryAsync(sql, param));
    }

    public static async Task<T> ExecuteScalarAsync<T>(this IDapperRepository repository, string sql,
        object? param = null)
    {
        return await HandleCommand<T>(repository,
            dbConnection => dbConnection.ExecuteScalarAsync<T>(sql, param));
    }
    
    public static async Task<int> ExecuteAsync(this IDapperRepository repository, string sql,
        object? param = null)
    {
        return await HandleCommand(repository,
            dbConnection => dbConnection.ExecuteAsync(sql, param));
    }

    private static async Task<T> HandleCommand<T>(IDapperRepository repository, Func<DbConnection, Task<T>> handler)
    {
        bool isDisposable = false;
        DbConnection? dbConnection = null;
        try
        {
            var (connection, isNew) = await GetOpenNpgSqlConnection(repository);
            isDisposable = isNew;
            dbConnection = connection;
            return await handler(connection);
        }
        finally
        {
            if (isDisposable && dbConnection != null)
            {
                await dbConnection.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Get an open NpgSqlConnection. This will be from DbContext if present, else created new.
    /// </summary>
    /// <param name="dapperRepository"></param>
    /// <returns>
    /// Open NpgSqlConnection object and boolean representing whether this connection was created or already in use.
    /// If created then it can be disposed, else it will be disposed of along with DbContext
    /// </returns>
    /// <exception cref="InvalidCastException"></exception>
    private static async Task<(NpgsqlConnection connection, bool wasCreated)> GetOpenNpgSqlConnection(IDapperRepository dapperRepository)
    {
        return dapperRepository switch
        {
            IDapperContextRepository contextRepo => (await contextRepo.DlcsContext.GetOpenNpgSqlConnection(), false),
            IDapperConfigRepository configRepo => (
                await DatabaseConnectionManager.GetOpenNpgSqlConnection(configRepo.Configuration), true),
            _ => throw new InvalidCastException("Cannot get source of Db connection from IDapperRepository")
        };
    }
}
