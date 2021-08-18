using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.S3;
using DLCS.Model.Assets;
using DLCS.Model.Customer;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;
using Xunit;

namespace Orchestrator.Tests.Integration
{
    /// <summary>
    /// Test of all requests handled by custom iiif-img handling
    /// </summary>
    [Trait("Category", "Integration")]
    [Collection(OrchestratorCollection.CollectionName)]
    public class FileHandlingTests : IClassFixture<ProtagonistAppFactory<Startup>>
    {
        private readonly OrchestratorFixture orchestratorFixture;
        private readonly DlcsDatabaseFixture dbFixture;
        private readonly HttpClient httpClient;
        private readonly IAmazonS3 amazonS3;
        private readonly string stubAddress;

        public FileHandlingTests(ProtagonistAppFactory<Startup> factory, OrchestratorFixture orchestratorFixture)
        {
            this.orchestratorFixture = orchestratorFixture;
            dbFixture = orchestratorFixture.DbFixture;
            amazonS3 = orchestratorFixture.LocalStackFixture.AmazonS3;
            stubAddress = orchestratorFixture.ApiStub.Address;
            httpClient = factory
                .WithConnectionString(dbFixture.ConnectionString)
                .WithLocalStack(orchestratorFixture.LocalStackFixture)
                .CreateClient(new WebApplicationFactoryClientOptions {AllowAutoRedirect = false});
            
            dbFixture.CleanUp();
            orchestratorFixture.WithTestFile();
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
            await dbFixture.DbContext.Images.AddAsync(new Asset
            {
                Created = DateTime.Now, Customer = 99, Space = 1, Id = "99/1/Get_NotFoundHttOrigin_Returns404",
                Origin = $"{stubAddress}/not-found", Family = 'F', MediaType = "image/jpeg",
                ThumbnailPolicy = "default"
            });
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
            await dbFixture.DbContext.Images.AddAsync(new Asset
            {
                Created = DateTime.Now, Customer = 99, Space = 1, Id = "99/1/Get_HttpOrigin_ReturnsFile",
                Origin = $"{stubAddress}/testfile", Family = 'F', MediaType = "image/jpeg",
                ThumbnailPolicy = "default"
            });
            await dbFixture.DbContext.SaveChangesAsync();

            // Act
            var response = await httpClient.GetAsync("file/99/1/Get_HttpOrigin_ReturnsFile");
            
            // Assert
            response.Content.Headers.ContentType.MediaType.Should().Be("application/pdf");
            response.Content.Headers.ContentLength.Should().BeGreaterThan(0);
        }
        
        [Fact(Skip = "Issue with Stubbery lifecycle - runs in isolation but fails in group run")]
        public async Task Get_BasicAuthHttpOrigin_ReturnsFile()
        {
            // Arrange
            await dbFixture.DbContext.Images.AddAsync(new Asset
            {
                Created = DateTime.Now, Customer = 99, Space = 1, Id = "99/1/Get_BasicAuthHttpOrigin_ReturnsFile",
                Origin = $"{stubAddress}/authfile", Family = 'F', MediaType = "image/jpeg",
                ThumbnailPolicy = "default"
            });
            await dbFixture.DbContext.CustomerOriginStrategies.AddRangeAsync(new CustomerOriginStrategy
            {
                Credentials = orchestratorFixture.ValidCreds, Customer = 99, Id = "basic-auth-file", 
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
            await dbFixture.DbContext.Images.AddAsync(new Asset
            {
                Created = DateTime.Now, Customer = 99, Space = 1, Id = "99/1/Get_BasicAuthHttpOrigin_BadCredentials_Returns404",
                Origin = $"{stubAddress}/forbiddenfile", Family = 'F', MediaType = "application/pdf",
                ThumbnailPolicy = "default"
            });
            await dbFixture.DbContext.CustomerOriginStrategies.AddRangeAsync(new CustomerOriginStrategy
            {
                Credentials = orchestratorFixture.ValidCreds, Customer = 99, Id = "basic-forbidden-file", 
                Strategy = OriginStrategyType.BasicHttp, Regex = $"{stubAddress}/forbiddenfile"
            });
            await dbFixture.DbContext.SaveChangesAsync();

            // Act
            var response = await httpClient.GetAsync("file/99/1/Get_BasicAuthHttpOrigin_BadCredentials_Returns404");
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}