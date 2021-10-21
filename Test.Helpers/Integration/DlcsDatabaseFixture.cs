﻿using System;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Security;
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
            DbContext.Database.ExecuteSqlRaw("DELETE FROM \"Spaces\" WHERE \"Customer\" != 99 AND \"Id\" != 1");
            DbContext.Database.ExecuteSqlRaw("DELETE FROM \"Customers\" WHERE \"Id\" != 99");
            DbContext.Database.ExecuteSqlRaw("DELETE FROM \"ThumbnailPolicies\" WHERE \"Id\" != 'default'");
            DbContext.Database.ExecuteSqlRaw("DELETE FROM \"Images\"");
            DbContext.Database.ExecuteSqlRaw("DELETE FROM \"CustomerOriginStrategies\"");
            DbContext.Database.ExecuteSqlRaw($"DELETE FROM \"AuthServices\" WHERE \"Id\" != '{ClickThroughAuthService}'");
            DbContext.Database.ExecuteSqlRaw("DELETE FROM \"Roles\" WHERE \"Id\" != 'clickthrough'");
            DbContext.Database.ExecuteSqlRaw("DELETE FROM \"SessionUsers\"");
            DbContext.Database.ExecuteSqlRaw("DELETE FROM \"AuthTokens\"");
        }

        public const string ClickThroughAuthService = "ba7fd6e2-773b-4ef2-bdb9-c8ee9b46fd54";

        private async Task SeedCustomer()
        {
            const int customer = 99;

            await DbContext.Customers.AddAsync(new Customer
            {
                Created = DateTime.Now,
                Id = customer,
                DisplayName = "TestUser",
                Name = "test",
                Keys = Array.Empty<string>()
            });
            await DbContext.Spaces.AddAsync(new Space
                { Created = DateTime.Now, Id = 1, Customer = customer, Name = "space-1" });
            await DbContext.ThumbnailPolicies.AddAsync(new ThumbnailPolicy
                { Id = "default", Name = "default", Sizes = "800,400,200" });
            await DbContext.AuthServices.AddAsync(new AuthService
            {
                Customer = customer, Name = "clickthrough", Id = ClickThroughAuthService,
                Description = "", Label = "", Profile = "", Ttl = 200, PageDescription = "",
                PageLabel = "", RoleProvider = "", CallToAction = "", ChildAuthService = ""
            });
            await DbContext.Roles.AddAsync(new Role
            {
                Customer = customer, Id = "clickthrough", AuthService = ClickThroughAuthService,
                Name = "test-clickthrough"
            });
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