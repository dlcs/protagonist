using Stubbery;
using Test.Helpers.Integration;

namespace Engine.Tests.Integration.Infrastructure;

/// <summary>
/// XUnit fixture that bootstraps postgres db, localstack and ApiStub
/// </summary>
public class EngineFixture : IAsyncLifetime
{
    public DlcsDatabaseFixture DbFixture { get; }
    public LocalStackFixture LocalStackFixture { get; }

    public ApiStub ApiStub { get; }

    public EngineFixture()
    {
        ApiStub = new ApiStub();
        DbFixture = new DlcsDatabaseFixture();
        LocalStackFixture = new LocalStackFixture();
    }
    
    public async Task InitializeAsync()
    {
        ApiStub.Start();
        await DbFixture.InitializeAsync();
        await LocalStackFixture.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        ApiStub.Dispose();
        await DbFixture.DisposeAsync();
        await LocalStackFixture.DisposeAsync();
    }
}