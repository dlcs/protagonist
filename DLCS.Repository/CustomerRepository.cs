using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DLCS.Model.Customer;
using DLCS.Model.PathElements;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository
{
    public class CustomerRepository : ICustomerRepository
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<CustomerRepository> logger;

        public CustomerRepository(IConfiguration configuration, ILogger<CustomerRepository> logger)
        {
            this.configuration = configuration;
            this.logger = logger;
        }

        public async Task<Dictionary<string, int>> GetCustomerIdLookup()
        {
            await using var connection = await DatabaseConnectionManager.GetOpenNpgSqlConnection(configuration);
            var results = await connection.QueryAsync<CustomerPathElement>("SELECT \"Id\", \"Name\" FROM \"Customers\"");
            return results.ToDictionary(cpe => cpe.Name, cpe => cpe.Id);
        }
    }
}
