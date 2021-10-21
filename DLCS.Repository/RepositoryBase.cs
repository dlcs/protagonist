using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;

namespace DLCS.Repository
{
    public abstract class RepositoryBase
    {
        protected readonly IConfiguration configuration;
        protected readonly DlcsContext dlcsContext;

        protected RepositoryBase(
            IConfiguration configuration,
            DlcsContext dlcsContext
        )
        {
            this.configuration = configuration;
            this.dlcsContext = dlcsContext;
        }

        protected async Task ExecuteSqlAsync(string sql, object param = null)
        {
            await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
            await connection.ExecuteAsync(sql, param);
        }

        protected async Task<T> QueryFirstOrDefaultAsync<T>(string sql, object param = null)
        {
            await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
            return await connection.QueryFirstOrDefaultAsync<T>(sql, param);
        }
        
    }
}