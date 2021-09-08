using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;
using Xunit;

namespace Orchestrator.Tests.Integration
{
    /// <summary>
    /// Test of all iiif-manifest handling
    /// </summary>
    [Trait("Category", "Integration")]
    [Collection(StorageCollection.CollectionName)]
    public class ManifestHandlingTests : IClassFixture<ProtagonistAppFactory<Startup>>
    {
        private readonly DlcsDatabaseFixture dbFixture;
        private readonly HttpClient httpClient;
        
        public ManifestHandlingTests(ProtagonistAppFactory<Startup> factory, StorageFixture storageFixture)
        {
            dbFixture = storageFixture.DbFixture;
            
            httpClient = factory
                .WithConnectionString(dbFixture.ConnectionString)
                .CreateClient();
            
            dbFixture.CleanUp();
        }
        
        [Fact]
        public async Task Get_UnknownCustomer_Returns404()
        {
            // Arrange
            const string path = "iiif-manifest/1/1/my-asset";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task Get_UnknownSpace_Returns404()
        {
            // Arrange
            const string path = "iiif-manifest/99/5/my-asset";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task Get_UnknownImage_Returns404()
        {
            // Arrange
            const string path = "iiif-manifest/99/1/my-asset";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}