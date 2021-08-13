using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Repository;
using DLCS.Repository.Entities;
using DotNet.Testcontainers.Containers.Builders;
using DotNet.Testcontainers.Containers.Configurations.Databases;
using DotNet.Testcontainers.Containers.Modules.Databases;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Test.Helpers.Integration
{
    /// <summary>
    /// Xunit fixture that manages lifecycle for Postgres 12 container with basic migration applied.
    /// Seeds Customer 99 with 1 space and default thumbnailPolicy
    /// </summary>
    public class DlcsDatabaseFixture : IAsyncLifetime
    {
        private readonly PostgreSqlTestcontainer postgresContainer;

        public DlcsContext DbContext { get; }
        public string ConnectionString { get; }
        
        public DlcsDatabaseFixture()
        {
            var postgresBuilder = new TestcontainersBuilder<PostgreSqlTestcontainer>()
                .WithDatabase(new PostgreSqlTestcontainerConfiguration("postgres:12-alpine")
                {
                    Database = "db",
                    Password = "postgres_pword",
                    Username = "postgres"
                })
                .WithCleanUp(true)
                .WithLabel("protagonist_test", "True");

            postgresContainer = postgresBuilder.Build();
            ConnectionString = postgresContainer.ConnectionString;

            // Create new DlcsContext using connection string for Postgres container
            DbContext = new DlcsContext(
                new DbContextOptionsBuilder<DlcsContext>()
                    .UseNpgsql(postgresContainer.ConnectionString).Options
            );
            DbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }
        
        /// <summary>
        /// Delete any standing data
        /// </summary>
        public void CleanUp()
        {
            DbContext.Database.ExecuteSqlRaw("DELETE FROM \"Spaces\" WHERE \"Customer\" != 99");
            DbContext.Database.ExecuteSqlRaw("DELETE FROM \"Customers\" WHERE \"Id\" != 99");
            DbContext.Database.ExecuteSqlRaw("DELETE FROM \"ThumbnailPolicies\" WHERE \"Id\" != 'default'");
            DbContext.Database.ExecuteSqlRaw("DELETE FROM \"Images\"");
            DbContext.Database.ExecuteSqlRaw("DELETE FROM \"CustomerOriginStrategies\"");
        }

        private async Task SeedCustomer()
        {
            const int customer = 99;
            
            await DbContext.Customers.AddAsync(new Customer
            {
                Created = DateTime.Now,
                Id = customer,
                DisplayName = "TestUser",
                Name = "test",
                Keys = ""
            });
            await DbContext.Spaces.AddAsync(new Space {Created = DateTime.Now, Id = 1, Customer = customer, Name = "space-1"});
            await  DbContext.ThumbnailPolicies.AddAsync(new ThumbnailPolicy
                {Id = "default", Name = "default", Sizes = "800,400,200"});
            await DbContext.SaveChangesAsync();
        }

        public async Task InitializeAsync()
        {
            // Start DB + apply migrations
            try
            {
                await postgresContainer.StartAsync();
                await DbContext.Database.MigrateAsync();
                await SeedCustomer();
            }
            catch (Exception ex)
            {
                var m = ex.Message;
                throw;
            }
        }

        public Task DisposeAsync() => postgresContainer.StopAsync();
    }
}