using System.Collections.Generic;
using System.Linq;
using Dapper;
using DLCS.Model.Customer;
using DLCS.Model.PathElements;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

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

        public Dictionary<string, int> GetCustomerIdLookup()
        {
            using (var connection = new NpgsqlConnection(configuration.GetConnectionString("PostgreSQLConnection")))
            {
                connection.Open();
                return connection
                    .Query<CustomerPathElement>("SELECT \"Id\", \"Name\" FROM \"Customers\"")
                    .ToDictionary(cpe => cpe.Name, cpe => cpe.Id);
            }
        }
    }
}
