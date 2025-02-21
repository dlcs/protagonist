using System.Threading.Tasks;
using Xunit;

namespace Test.Helpers.Integration;

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