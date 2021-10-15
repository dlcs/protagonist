using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DLCS.Model.Assets.NamedQueries;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;
using Xunit;

namespace Orchestrator.Tests.Integration
{
    [Trait("Category", "Integration")]
    [Collection(DatabaseCollection.CollectionName)]
    public class NamedQueryTests: IClassFixture<ProtagonistAppFactory<Startup>>
    {
        private readonly DlcsDatabaseFixture dbFixture;
        private readonly HttpClient httpClient;

        public NamedQueryTests(ProtagonistAppFactory<Startup> factory, DlcsDatabaseFixture databaseFixture)
        {
            dbFixture = databaseFixture;
            httpClient = factory
                .WithConnectionString(dbFixture.ConnectionString)
                .CreateClient();

            dbFixture.CleanUp();

            // Setup a basic NQ for testing
            dbFixture.DbContext.NamedQueries.Add(new NamedQuery
            {
                Customer = 99, Global = false, Id = Guid.NewGuid().ToString(), Name = "test-named-query",
                Template = "canvas=n1&s1=p1&space=p2"
            });

            dbFixture.DbContext.Images.AddTestAsset("99/1/matching-1", num1: 2, ref1: "my-ref");
            dbFixture.DbContext.Images.AddTestAsset("99/1/matching-2", num1: 1, ref1: "my-ref");
            dbFixture.DbContext.Images.AddTestAsset("99/1/matching-nothumbs", num1: 3, ref1: "my-ref",
                maxUnauthorised: 10, roles: "default");
            dbFixture.DbContext.SaveChanges();
        }

        [Theory]
        [InlineData("iiif-resource/99/unknown-nq")]
        [InlineData("iiif-resource/99/unknown-nq/v2")]
        [InlineData("iiif-resource/99/unknown-nq/v3")]
        public async Task Get_Returns404_IfNQNotFound(string path)
        {
            // Act
            var response = await httpClient.GetAsync(path);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Theory]
        [InlineData("iiif-resource/98/test-named-query")]
        [InlineData("iiif-resource/98/test-named-query/v2")]
        [InlineData("iiif-resource/98/test-named-query/v3")]
        public async Task Get_Returns404_IfCustomerNotFound(string path)
        {
            // Act
            var response = await httpClient.GetAsync(path);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Theory]
        [InlineData("iiif-resource/99/test-named-query/too-little-params")]
        [InlineData("iiif-resource/99/test-named-query/v2/too-little-params")]
        [InlineData("iiif-resource/99/test-named-query/v3/too-little-params")]
        public async Task Get_Returns400_IfNamedQueryParametersIncorrect(string path)
        {
            // Act
            var response = await httpClient.GetAsync(path);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Get_ReturnsV2ManifestWithCorrectCount_ViaConneg()
        {
            // Arrange
            const string path = "iiif-resource/99/test-named-query/my-ref/1";
            const string iiif2 = "application/ld+json; profile=\"http://iiif.io/api/presentation/2/context.json\"";
            
            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Accept", iiif2);
            var response = await httpClient.SendAsync(request);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType.ToString().Should().Be(iiif2);
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse.SelectToken("sequences[0].canvases").Count().Should().Be(3);
        }
        
        [Fact]
        public async Task Get_ReturnsV2ManifestWithCorrectCount_ViaDirectPath()
        {
            // Arrange
            const string path = "iiif-resource/99/test-named-query/v2/my-ref/1";
            const string iiif2 = "application/ld+json; profile=\"http://iiif.io/api/presentation/2/context.json\"";
            
            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType.ToString().Should().Be(iiif2);
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse.SelectToken("sequences[0].canvases").Count().Should().Be(3);
        }
        
        [Fact]
        public async Task Get_ReturnsV3ManifestWithCorrectCount_ViaConneg()
        {
            // Arrange
            const string path = "iiif-resource/99/test-named-query/my-ref/1";
            const string iiif2 = "application/ld+json; profile=\"http://iiif.io/api/presentation/3/context.json\"";
            
            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Accept", iiif2);
            var response = await httpClient.SendAsync(request);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType.ToString().Should().Be(iiif2);
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse.SelectToken("items").Count().Should().Be(3);
        }
        
        [Fact]
        public async Task Get_ReturnsV3ManifestWithCorrectCount_ViaDirectPath()
        {
            // Arrange
            const string path = "iiif-resource/99/test-named-query/v3/my-ref/1";
            const string iiif2 = "application/ld+json; profile=\"http://iiif.io/api/presentation/3/context.json\"";
            
            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType.ToString().Should().Be(iiif2);
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse.SelectToken("items").Count().Should().Be(3);
        }
        
        [Fact]
        public async Task Get_ReturnsV3ManifestWithCorrectCount_AsCanonical()
        {
            // Arrange
            const string path = "iiif-resource/99/test-named-query/my-ref/1";
            const string iiif2 = "application/ld+json; profile=\"http://iiif.io/api/presentation/3/context.json\"";
            
            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType.ToString().Should().Be(iiif2);
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse.SelectToken("items").Count().Should().Be(3);
        }
    }
}