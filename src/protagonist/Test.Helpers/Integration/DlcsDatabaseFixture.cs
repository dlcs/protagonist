using System;
using System.Threading.Tasks;
using DLCS.Model.Auth.Entities;
using DLCS.Model.Customers;
using DLCS.Model.Policies;
using DLCS.Model.Spaces;
using DLCS.Model.Storage;
using DLCS.Repository;
using DLCS.Repository.Entities;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Test.Helpers.Integration;

/// <summary>
/// Xunit fixture that manages lifecycle for Postgres 12 container with basic migration applied.
/// Seeds Customer 99 with 1 space and default thumbnailPolicy
/// </summary>
public class DlcsDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestcontainer postgresContainer;

    public DlcsContext DbContext { get; private set; }
    public string ConnectionString { get; private set; }

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
    }

    /// <summary>
    /// Delete any standing data - leaves data set in Seed method
    /// </summary>
    public void CleanUp()
    {
        DbContext.Database.ExecuteSqlRaw("DELETE FROM \"Spaces\" WHERE \"Customer\" != 99 AND \"Id\" != 1");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM \"Customers\" WHERE \"Id\" != 99");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM \"StoragePolicies\" WHERE \"Id\" not in ('default', 'small', 'medium')");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM \"ThumbnailPolicies\" WHERE \"Id\" != 'default'");
        DbContext.Database.ExecuteSqlRaw(
            "DELETE FROM \"ImageOptimisationPolicies\" WHERE \"Id\" not in ('fast-higher', 'video-max', 'audio-max', 'cust-default')");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM \"Images\"");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM \"CustomerOriginStrategies\"");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM \"CustomerStorage\"");
        DbContext.Database.ExecuteSqlRaw($"DELETE FROM \"AuthServices\" WHERE \"Id\" != '{ClickThroughAuthService}'");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM \"Roles\" WHERE \"Id\" != 'clickthrough'");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM \"SessionUsers\"");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM \"AuthTokens\"");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM \"Batches\"");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM \"Queues\"");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM \"EntityCounters\" WHERE \"Type\" = 'space' AND \"Customer\" != 99");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM \"EntityCounters\" WHERE \"Type\" = 'space-images' AND \"Customer\" != 99");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM \"EntityCounters\" WHERE \"Type\" = 'customer-images' AND \"Scope\" != '99'");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM \"DeliveryChannelPolicies\" WHERE \"Customer\" <> 1");
        DbContext.Database.ExecuteSqlRaw("DELETE FROM \"DefaultDeliveryChannels\" WHERE \"Customer\" <> 1");
        DbContext.ChangeTracker.Clear();
    }

    public const string ClickThroughAuthService = "ba7fd6e2-773b-4ef2-bdb9-c8ee9b46fd54";

    private async Task SeedCustomer()
    {
        const int customer = 99;
        const int adminCustomer = 1;
        await DbContext.Customers.AddAsync(new Customer
        {
            Created = DateTime.UtcNow,
            Id = customer,
            DisplayName = "TestUser",
            Name = "test",
            Keys = Array.Empty<string>()
        });
        await DbContext.Customers.AddAsync(new Customer()
        {
            Id = adminCustomer,
            Name = "admin",
            DisplayName = "admin customer",
            Created = DateTime.UtcNow,
            Keys = new[] { "some", "keys" },
            Administrator = true,
            AcceptedAgreement = true
        });

        await DbContext.EntityCounters.AddAsync(new EntityCounter()
        {
            Customer = 0,
            Next = 2,
            Scope = "0",
            Type = "customer"
        });
        
        await DbContext.StoragePolicies.AddRangeAsync(new StoragePolicy
            {
                Id = "default",
                MaximumNumberOfStoredImages = 1000000,
                MaximumTotalSizeOfStoredImages = 1000000000
            },
            new StoragePolicy
            {
                Id = "small",
                MaximumNumberOfStoredImages = 10,
                MaximumTotalSizeOfStoredImages = 100
            },
            new StoragePolicy
            {
                Id = "medium",
                MaximumNumberOfStoredImages = 100,
                MaximumTotalSizeOfStoredImages = 1000
            });
        
        await DbContext.EntityCounters.AddRangeAsync(new EntityCounter
        {
            Type = KnownEntityCounters.CustomerSpaces,
            Customer = customer,
            Scope = customer.ToString(),
            Next = 1
        }, new EntityCounter
        {
            Type = KnownEntityCounters.SpaceImages,
            Customer = customer,
            Scope = "1",
            Next = 1
        }, new EntityCounter
        {
            Type = KnownEntityCounters.CustomerImages,
            Customer = 0,
            Scope = customer.ToString(),
            Next = 1
        });
        await DbContext.Spaces.AddAsync(new Space
            { Created = DateTime.UtcNow, Id = 1, Customer = customer, Name = "space-1" });
        await DbContext.ThumbnailPolicies.AddAsync(new ThumbnailPolicy
            { Id = "default", Name = "default", Sizes = "800,400,200" });
        await DbContext.ImageOptimisationPolicies.AddRangeAsync(
            new ImageOptimisationPolicy
            {
                Id = "video-max", Name = "Video", TechnicalDetails = new[] { "System preset: Webm 720p(webm)" },
                Global = true
            },
            new ImageOptimisationPolicy
            {
                Id = "audio-max", Name = "Audio", TechnicalDetails = new[] { "System preset: Audio MP3 - 128k(mp3)" },
                Global = true
            },
            new ImageOptimisationPolicy
            {
                Id = "fast-higher", Name = "Fast higher quality", TechnicalDetails = new[] { "kdu_max" }, Global = true
            },
            new ImageOptimisationPolicy
            {
                Id = "cust-default", Name = "Customer Scoped", TechnicalDetails = new[] { "default" },
                Global = false, Customer = 99
            });
        await DbContext.AuthServices.AddAsync(new AuthService
        {
            Customer = customer, Name = "clickthrough", Id = ClickThroughAuthService, Description = "", Label = "",
            Profile = "http://iiif.io/api/auth/1/clickthrough", Ttl = 200, PageDescription = "", PageLabel = "",
            RoleProvider = "", CallToAction = "", ChildAuthService = ""
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
            SetPropertiesFromContainer();
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
    
    private void SetPropertiesFromContainer()
    {
        ConnectionString = postgresContainer.ConnectionString;

        // Create new DlcsContext using connection string for Postgres container
        DbContext = new DlcsContext(
            new DbContextOptionsBuilder<DlcsContext>()
                .UseNpgsql(postgresContainer.ConnectionString).Options
        );
        DbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }
}