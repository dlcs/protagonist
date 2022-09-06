using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DLCS.Repository;

public abstract class DapperRepository
{
    private readonly DlcsContext? dlcsContext;
    private readonly IConfiguration? configuration;

    /// <summary>
    /// Get new DapperRepository object using IConfiguration to access connection string
    /// </summary>
    protected DapperRepository(IConfiguration configuration)
    {
        this.configuration = configuration;
    }
    
    /// <summary>
    /// Get new DapperRepository object using DlcsContext to access connection string
    /// </summary>
    protected DapperRepository(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }

    protected async Task ExecuteSqlAsync(string sql, object? param = null)
    {
        await using var connection = await GetOpenNpgSqlConnection();
        await connection.ExecuteAsync(sql, param);
    }

    protected async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null)
    {
        await using var connection = await GetOpenNpgSqlConnection();
        return await connection.QueryFirstOrDefaultAsync<T?>(sql, param);
    }

    protected async Task<dynamic?> QueryFirstOrDefaultAsync(string sql, object? param = null)
    {
        await using var connection = await GetOpenNpgSqlConnection();
        return await connection.QuerySingleOrDefaultAsync(sql, param);
    }

    protected async Task<T> QuerySingleOrDefaultAsync<T>(string sql, object? param = null)
    {
        await using var connection = await GetOpenNpgSqlConnection();
        return await connection.QueryFirstOrDefaultAsync<T>(sql, param);
    }

    protected async Task<dynamic?> QuerySingleOrDefaultAsync(string sql, object? param = null)
    {
        await using var connection = await GetOpenNpgSqlConnection();
        return await connection.QuerySingleOrDefaultAsync(sql, param);
    }

    protected async Task<T> QuerySingleAsync<T>(string sql, object? param = null)
    {
        await using var connection = await GetOpenNpgSqlConnection();
        return await connection.QuerySingleAsync<T>(sql, param);
    }

    protected async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
    {
        await using var connection = await GetOpenNpgSqlConnection();
        return await connection.QueryAsync<T>(sql, param);
    }

    protected async Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param = null)
    {
        await using var connection = await GetOpenNpgSqlConnection();
        return await connection.QueryAsync(sql, param);
    }

    protected async Task<T> ExecuteScalarAsync<T>(string sql, object? param = null)
    {
        await using var connection = await GetOpenNpgSqlConnection();
        return await connection.ExecuteScalarAsync<T>(sql, param);
    }
    
    private Task<NpgsqlConnection> GetOpenNpgSqlConnection()
        => dlcsContext != null
            ? dlcsContext.GetOpenNpgSqlConnection()
            : DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration!);
}