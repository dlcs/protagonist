using System;
using System.Collections.Generic;
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
    public static async Task ExecuteSqlAsync(this IDapperRepository repository, string sql, object? param = null)
    {
        await using var connection = await GetOpenNpgSqlConnection(repository);
        await connection.ExecuteAsync(sql, param);
    }

    public static async Task<T?> QueryFirstOrDefaultAsync<T>(this IDapperRepository repository, string sql,
        object? param = null)
    {
        await using var connection = await GetOpenNpgSqlConnection(repository);
        return await connection.QueryFirstOrDefaultAsync<T?>(sql, param);
    }

    public static async Task<dynamic?> QueryFirstOrDefaultAsync(this IDapperRepository repository, string sql,
        object? param = null)
    {
        await using var connection = await GetOpenNpgSqlConnection(repository);
        return await connection.QuerySingleOrDefaultAsync(sql, param);
    }

    public static async Task<T?> QuerySingleOrDefaultAsync<T>(this IDapperRepository repository, string sql,
        object? param = null)
    {
        await using var connection = await GetOpenNpgSqlConnection(repository);
        return await connection.QueryFirstOrDefaultAsync<T>(sql, param);
    }

    public static async Task<dynamic?> QuerySingleOrDefaultAsync(this IDapperRepository repository, string sql,
        object? param = null)
    {
        await using var connection = await GetOpenNpgSqlConnection(repository);
        return await connection.QuerySingleOrDefaultAsync(sql, param);
    }

    public static async Task<T> QuerySingleAsync<T>(this IDapperRepository repository, string sql, object? param = null)
    {
        await using var connection = await GetOpenNpgSqlConnection(repository);
        return await connection.QuerySingleAsync<T>(sql, param);
    }

    public static async Task<IEnumerable<T>> QueryAsync<T>(this IDapperRepository repository, string sql,
        object? param = null)
    {
        await using var connection = await GetOpenNpgSqlConnection(repository);
        return await connection.QueryAsync<T>(sql, param);
    }

    public static async Task<IEnumerable<dynamic>> QueryAsync(this IDapperRepository repository, string sql,
        object? param = null)
    {
        await using var connection = await GetOpenNpgSqlConnection(repository);
        return await connection.QueryAsync(sql, param);
    }

    public static async Task<T> ExecuteScalarAsync<T>(this IDapperRepository repository, string sql,
        object? param = null)
    {
        await using var connection = await GetOpenNpgSqlConnection(repository);
        return await connection.ExecuteScalarAsync<T>(sql, param);
    }

    private static Task<NpgsqlConnection> GetOpenNpgSqlConnection(IDapperRepository dapperRepository)
        => dapperRepository switch
        {
            IDapperContextRepository contextRepo => contextRepo.DlcsContext.GetOpenNpgSqlConnection(),
            IDapperConfigRepository configRepo => DatabaseConnectionManager.GetOpenNpgSqlConnection(configRepo
                .Configuration),
            _ => throw new InvalidCastException("Cannot get source of Db connection from IDapperRepository")
        };
}