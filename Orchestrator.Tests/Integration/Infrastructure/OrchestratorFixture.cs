using System.Reflection;
using System.Threading.Tasks;
using DLCS.Model.Security;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Stubbery;
using Test.Helpers.Integration;
using Xunit;

namespace Orchestrator.Tests.Integration.Infrastructure
{
    /// <summary>
    /// XUnit fixture that bootstraps postgres db, localstack and ApiStub
    /// </summary>
    public class OrchestratorFixture : IAsyncLifetime
    {
        public DlcsDatabaseFixture DbFixture { get; }
        public LocalStackFixture LocalStackFixture { get; }

        public ApiStub ApiStub { get; }

        public OrchestratorFixture()
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
}