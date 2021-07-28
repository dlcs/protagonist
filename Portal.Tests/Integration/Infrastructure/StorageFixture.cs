using System.Threading.Tasks;
using Test.Helpers.Integration;
using Xunit;

namespace Portal.Tests.Integration.Infrastructure
{
    /// <summary>
    /// Xunit fixture that contains both DbFixture and LocalStackFixture
    /// </summary>
    public class StorageFixture : IAsyncLifetime
    {
        public DlcsDatabaseFixture DbFixture { get; }
        public LocalStackFixture LocalStackFixture { get; }

        public StorageFixture()
        {
            DbFixture = new DlcsDatabaseFixture();
            LocalStackFixture = new LocalStackFixture();
        }
        
        public async Task InitializeAsync()
        {
            await DbFixture.InitializeAsync();
            await LocalStackFixture.InitializeAsync();
        }

        public async Task DisposeAsync()
        {
            await DbFixture.DisposeAsync();
            await LocalStackFixture.DisposeAsync();
        }
    }
    
    [CollectionDefinition(CollectionName)]
    public class StorageCollection : ICollectionFixture<StorageFixture>
    {
        public const string CollectionName = "Storage Collection";
    }
}