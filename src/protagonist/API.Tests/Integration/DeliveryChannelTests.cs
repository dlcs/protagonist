using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.HydraModel;
using DLCS.Repository;
using Hydra.Collections;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class DeliveryChannelTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;

    public  DeliveryChannelTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
    {
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(dbFixture, "API-Test");
        dbFixture.CleanUp();
    }

    [Fact]
    public async Task Get_DeliveryChannelPolicy_200()
    {
        // Arrange
        var path = $"customers/99/deliveryChannelPolicies/thumbs/example-thumbs-policy";
  
        // Act
        var response = await httpClient.AsCustomer(99).GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var model = await response.ReadAsHydraResponseAsync<DeliveryChannelPolicy>();
        model.Name.Should().Be("example-thumbs-policy");
        model.DisplayName.Should().Be("Example Thumbnail Policy");
        model.Channel.Should().Be("thumbs");
        model.PolicyData.Should().Be("{[\"!1024,1024\",\"!400,400\",\"!200,200\",\"!100,100\"]}");
    }
    
    [Fact]
    public async Task Get_DeliveryChannelPolicy_404_IfNotFound()
    {
        // Arrange
        var path = $"customers/99/deliveryChannelPolicies/thumbs/foofoo";

        // Act
        var response = await httpClient.AsCustomer(99).GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}