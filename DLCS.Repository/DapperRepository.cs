using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;

namespace DLCS.Repository
{
    public abstract class DapperRepository
    {
        private readonly IConfiguration configuration;

        protected DapperRepository(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        protected async Task ExecuteSqlAsync(string sql, object param = null)
        {
            await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
            await connection.ExecuteAsync(sql, param);
        }

        protected async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object param = null)
        {
            await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
            return await connection.QueryFirstOrDefaultAsync<T?>(sql, param);
        }
        
        protected async Task<dynamic?> QueryFirstOrDefaultAsync(string sql, object param = null)
        {
            await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
            return await connection.QuerySingleOrDefaultAsync(sql, param);
        }
        
        
        protected async Task<T> QuerySingleOrDefaultAsync<T>(string sql, object param = null)
        {
            await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
            return await connection.QueryFirstOrDefaultAsync<T>(sql, param);
        }
        
        protected async Task<dynamic?> QuerySingleOrDefaultAsync(string sql, object param = null)
        {
            await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
            return await connection.QuerySingleOrDefaultAsync(sql, param);
        }
        
        
        protected async Task<T> QuerySingleAsync<T>(string sql, object param = null)
        {
            await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
            return await connection.QuerySingleAsync<T>(sql, param);
        }
        
        
        protected async Task<IEnumerable<T>> QueryAsync<T>(string sql, object param = null)
        {
            await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
            return await connection.QueryAsync<T>(sql, param);
        }
        
        protected async Task<IEnumerable<dynamic>> QueryAsync(string sql, object param = null)
        {
            await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
            return await connection.QueryAsync(sql, param);
        }
        
    }
}