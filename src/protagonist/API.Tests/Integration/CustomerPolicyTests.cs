using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.HydraModel;
using Hydra.Collections;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class CustomerPolicyTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;

    public CustomerPolicyTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
    {
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(dbFixture, "API-Test");
        dbFixture.CleanUp();
    }
    
    [Fact]
    public async Task Get_ImageOptimisationPolicies_200()
    {
        // Arrange
        var path = "customers/99/imageOptimisationPolicies";

        // Act
        var response = await httpClient.AsCustomer(99).GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var model = await response.ReadAsHydraResponseAsync<HydraCollection<ImageOptimisationPolicy>>();
        model.Members.Should().HaveCount(4);
    }
    
    [Fact]
    public async Task Get_ImageOptimisationPolicies_SupportsPaging()
    {
        // Arrange
        var path = "customers/99/imageOptimisationPolicies?pageSize=2";

        // Act
        var response = await httpClient.AsCustomer(99).GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var model = await response.ReadAsHydraResponseAsync<HydraCollection<ImageOptimisationPolicy>>();
        model.Members.Should().HaveCount(2);
    }
    
    [Fact]
    public async Task Get_ImageOptimisationPolicy_404_Global()
    {
        // Arrange
        var path = "customers/99/imageOptimisationPolicies/video-max";

        // Act
        var response = await httpClient.AsCustomer(99).GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_ImageOptimisationPolicy_200_CustomerSpecific()
    {
        // Arrange
        var path = "customers/99/imageOptimisationPolicies/cust-default";

        // Act
        var response = await httpClient.AsCustomer(99).GetAsync(path);

        // Assert
        var model = await response.ReadAsHydraResponseAsync<ImageOptimisationPolicy>();
        model.TechnicalDetails.Should().BeEquivalentTo("default");
    }

    [Fact]
    public async Task Get_ImageOptimisationPolicy_404_IfNotFound()
    {
        // Arrange
        var path = "customers/99/imageOptimisationPolicies/foofoo";

        // Act
        var response = await httpClient.AsCustomer(99).GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}