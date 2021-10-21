using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.S3;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Security;
using FluentAssertions;
using LazyCache;
using LazyCache.Mocks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;
using Xunit;

namespace Orchestrator.Tests.Integration
{
    /// <summary>
    /// Tests of all /file/ requests
    /// </summary>
    [Trait("Category", "Integration")]
    [Collection(OrchestratorCollection.CollectionName)]
    public class FileHandlingTests : IClassFixture<ProtagonistAppFactory<Startup>>
    {
        private readonly DlcsDatabaseFixture dbFixture;
        private readonly HttpClient httpClient;
        private readonly string stubAddress;
        
        public const string ValidAuth = "Basic dW5hbWU6cHdvcmQ=";

        public string ValidCreds =
            JsonConvert.SerializeObject(new BasicCredentials { Password = "pword", User = "uname" });

        public FileHandlingTests(ProtagonistAppFactory<Startup> factory, OrchestratorFixture orchestratorFixture)
        {
            dbFixture = orchestratorFixture.DbFixture;
            stubAddress = orchestratorFixture.ApiStub.Address;
            httpClient = factory
                .WithConnectionString(dbFixture.ConnectionString)
                .WithLocalStack(orchestratorFixture.LocalStackFixture)
                .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            dbFixture.CleanUp();
            ConfigureStubbery(orchestratorFixture);
        }

        [Fact]
        public async Task Get_UnknownCustomer_Returns404()
        {
            // Arrange
            const string path = "file/1/1/my-file.pdf";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task Get_UnknownSpace_Returns404()
        {
            // Arrange
            const string path = "file/99/5/my-file.pdf";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task Get_UnknownImage_Returns404()
        {
            // Arrange
            const string path = "file/99/1/my-file.pdf";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task Get_NotFoundHttOrigin_Returns404()
        {
            // Arrange
            var id = "99/1/Get_NotFoundHttOrigin_Returns404";
            await dbFixture.DbContext.Images.AddTestAsset(id, family: AssetFamily.File, mediaType: "application/pdf",
                origin: $"{stubAddress}/not-found");
            await dbFixture.DbContext.SaveChangesAsync();

            // Act
            var response = await httpClient.GetAsync("file/99/1/Get_DefaultOrigin_ReturnsFile");
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task Get_HttpOrigin_ReturnsFile()
        {
            // Arrange
            var id = "99/1/Get_HttpOrigin_ReturnsFile";
            await dbFixture.DbContext.Images.AddTestAsset(id, family: AssetFamily.File, mediaType: "application/pdf",
                origin: $"{stubAddress}/testfile");
            await dbFixture.DbContext.SaveChangesAsync();

            // Act
            var response = await httpClient.GetAsync("file/99/1/Get_HttpOrigin_ReturnsFile");
            
            // Assert
            response.Content.Headers.ContentType.MediaType.Should().Be("application/pdf");
            response.Content.Headers.ContentLength.Should().BeGreaterThan(0);
        }
        
        [Fact]
        public async Task Get_BasicAuthHttpOrigin_ReturnsFile()
        {
            // Arrange
            var id = "99/1/Get_BasicAuthHttpOrigin_ReturnsFile";
            await dbFixture.DbContext.Images.AddTestAsset(id, family: AssetFamily.File, mediaType: "application/pdf",
                origin: $"{stubAddress}/authfile");
            await dbFixture.DbContext.CustomerOriginStrategies.AddAsync(new CustomerOriginStrategy
            {
                Credentials = ValidCreds, Customer = 99, Id = "basic-auth-file", 
                Strategy = OriginStrategyType.BasicHttp, Regex = $"{stubAddress}/authfile"
            });
            await dbFixture.DbContext.SaveChangesAsync();

            // Act
            var response = await httpClient.GetAsync("file/99/1/Get_BasicAuthHttpOrigin_ReturnsFile");
            
            // Assert
            response.Content.Headers.ContentType.MediaType.Should().Be("application/pdf");
            response.Content.Headers.ContentLength.Should().BeGreaterThan(0);
        }
        
        [Fact]
        public async Task Get_BasicAuthHttpOrigin_BadCredentials_Returns404()
        {
            // Arrange
            var id = "99/1/Get_BasicAuthHttpOrigin_BadCredentials_Returns404";
            await dbFixture.DbContext.Images.AddTestAsset(id, family: AssetFamily.File, mediaType: "application/pdf",
                origin: $"{stubAddress}/forbiddenfile");
            await dbFixture.DbContext.CustomerOriginStrategies.AddAsync(new CustomerOriginStrategy
            {
                Credentials = ValidCreds, Customer = 99, Id = "basic-forbidden-file", 
                Strategy = OriginStrategyType.BasicHttp, Regex = $"{stubAddress}/forbiddenfile"
            });
            await dbFixture.DbContext.SaveChangesAsync();

            // Act
            var response = await httpClient.GetAsync("file/99/1/Get_BasicAuthHttpOrigin_BadCredentials_Returns404");
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        private static void ConfigureStubbery(OrchestratorFixture orchestratorFixture)
        {
            orchestratorFixture.ApiStub.Get("/testfile", (request, args) => "anything")
                .Header("Content-Type", "application/pdf");

            orchestratorFixture.ApiStub.Get("/authfile", (request, args) => "anything")
                .Header("Content-Type", "application/pdf")
                .IfHeader("Authorization", ValidAuth);

            orchestratorFixture.ApiStub.Get("/forbiddenfile", (request, args) => new ForbidResult());
        }
    }
}