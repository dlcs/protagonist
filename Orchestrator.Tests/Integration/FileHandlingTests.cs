using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.S3;
using DLCS.Model.Assets;
using DLCS.Model.Auth;
using DLCS.Model.Customers;
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
        public async Task Options_Returns200_WithCorsHeaders()
        {
            // Arrange
            const string path = "file/1/1/my-file.pdf";

            // Act
            var request = new HttpRequestMessage(HttpMethod.Options, path);
            var response = await httpClient.SendAsync(request);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
            response.Headers.Should().ContainKey("Access-Control-Allow-Headers");
            response.Headers.Should().ContainKey("Access-Control-Allow-Methods");
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
        public async Task Get_NotFoundHttpOrigin_Returns404()
        {
            // Arrange
            var id = $"99/1/{nameof(Get_NotFoundHttpOrigin_Returns404)}";
            await dbFixture.DbContext.Images.AddTestAsset(id, family: AssetFamily.File, mediaType: "application/pdf",
                origin: $"{stubAddress}/not-found");
            await dbFixture.DbContext.SaveChangesAsync();

            // Act
            var response = await httpClient.GetAsync($"file/{id}");
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task Get_Returns404_IfNotForDelivery()
        {
            // Arrange
            var id = $"99/1/{nameof(Get_Returns404_IfNotForDelivery)}";
            await dbFixture.DbContext.Images.AddTestAsset(id, notForDelivery: true);
            await dbFixture.DbContext.SaveChangesAsync();

            // Act
            var response = await httpClient.GetAsync($"file/{id}");
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task Get_HttpOrigin_ReturnsFile()
        {
            // Arrange
            var id = $"99/1/{nameof(Get_HttpOrigin_ReturnsFile)}";
            await dbFixture.DbContext.Images.AddTestAsset(id, family: AssetFamily.File, mediaType: "application/pdf",
                origin: $"{stubAddress}/testfile");
            await dbFixture.DbContext.SaveChangesAsync();

            // Act
            var response = await httpClient.GetAsync($"file/{id}");
            
            // Assert
            response.Content.Headers.ContentType.MediaType.Should().Be("application/pdf");
            response.Content.Headers.ContentLength.Should().BeGreaterThan(0);
        }
        
        [Fact]
        public async Task Get_BasicAuthHttpOrigin_ReturnsFile()
        {
            // Arrange
            var id = $"99/1/{nameof(Get_BasicAuthHttpOrigin_ReturnsFile)}";
            await dbFixture.DbContext.Images.AddTestAsset(id, family: AssetFamily.File, mediaType: "application/pdf",
                origin: $"{stubAddress}/authfile");
            await dbFixture.DbContext.CustomerOriginStrategies.AddAsync(new CustomerOriginStrategy
            {
                Credentials = ValidCreds, Customer = 99, Id = "basic-auth-file", 
                Strategy = OriginStrategyType.BasicHttp, Regex = $"{stubAddress}/authfile"
            });
            await dbFixture.DbContext.SaveChangesAsync();

            // Act
            var response = await httpClient.GetAsync($"file/{id}");
            
            // Assert
            response.Content.Headers.ContentType.MediaType.Should().Be("application/pdf");
            response.Content.Headers.ContentLength.Should().BeGreaterThan(0);
        }
        
        [Fact]
        public async Task Get_BasicAuthHttpOrigin_BadCredentials_Returns404()
        {
            // Arrange
            var id = $"99/1/{nameof(Get_BasicAuthHttpOrigin_BadCredentials_Returns404)}";
            await dbFixture.DbContext.Images.AddTestAsset(id, family: AssetFamily.File, mediaType: "application/pdf",
                origin: $"{stubAddress}/forbiddenfile");
            await dbFixture.DbContext.CustomerOriginStrategies.AddAsync(new CustomerOriginStrategy
            {
                Credentials = ValidCreds, Customer = 99, Id = "basic-forbidden-file", 
                Strategy = OriginStrategyType.BasicHttp, Regex = $"{stubAddress}/forbiddenfile"
            });
            await dbFixture.DbContext.SaveChangesAsync();

            // Act
            var response = await httpClient.GetAsync($"file/{id}");
            
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