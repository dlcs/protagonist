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
        
        public const string ValidAuth = "Basic dW5hbWU6cHdvcmQ=";

        public string ValidCreds =
            JsonConvert.SerializeObject(new BasicCredentials { Password = "pword", User = "uname" });

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

        /// <summary>
        /// Setup /testfile to return a sample PDF and /authfile to require basic Auth.
        /// </summary>
        public void WithTestFile()
        {
            var assembly = GetType().GetTypeInfo().Assembly;
            ApiStub.Get("/testfile",
                (request, args) =>
                    new FileStreamResult(
                        assembly.GetManifestResourceStream("Orchestrator.Tests.Integration.Files.dummy.pdf"),
                        "application/pdf"));

            ApiStub
                .Get("/authfile",
                    (request, args) =>
                        new FileStreamResult(
                            assembly.GetManifestResourceStream("Orchestrator.Tests.Integration.Files.dummy.pdf"),
                            "application/pdf"))
                .IfHeader("Authorization", ValidAuth);

            ApiStub.Get("/forbiddenfile", (request, args) => new ForbidResult());
        }
    }
}