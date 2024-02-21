using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.HydraModel;
using DLCS.Repository;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class DeliveryChannelTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    private readonly DlcsContext dbContext;
    
    public DeliveryChannelTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
    {
        dbContext = dbFixture.DbContext;
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
    
    [Fact]
    public async Task Post_DeliveryChannelPolicy_201()
    {
        // Arrange
        const int customerId = 88;
        const string newDeliveryChannelPolicyJson = @"{
            ""name"": ""my-iiif-av-policy-1"",
            ""displayName"": ""My IIIF AV Policy"",
            ""policyData"": ""[\""audio-mp3-128\""]""
        }";
        
        var path = $"customers/{customerId}/deliveryChannelPolicies/iiif-av";

        // Act
        var content = new StringContent(newDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var foundPolicy = dbContext.DeliveryChannelPolicies.Single(s => 
            s.Customer == customerId &&
            s.Name == "my-iiif-av-policy-1");
        foundPolicy.DisplayName.Should().Be("My IIIF AV Policy");
        foundPolicy.PolicyData.Should().Be("[\"audio-mp3-128\"]");
    }
    
    [Fact]
    public async Task Put_DeliveryChannelPolicy_201()
    {
        // Arrange
        const int customerId = 88;
        const string putDeliveryChannelPolicyJson = @"{
            ""displayName"": ""My IIIF AV Policy 2 (modified)"",
            ""policyData"": ""[\""audio-mp3-256\""]""
        }";
        
        var policy = new DLCS.Model.Policies.DeliveryChannelPolicy()
        {
            Customer = customerId,
            Name = "put-av-policy-2",
            DisplayName = "My IIIF-AV Policy 2",
            Channel = "iiif-av",
            PolicyData = "[\"audio-mp3-128\"]"
        };
        
        var path = $"customers/{customerId}/deliveryChannelPolicies/{policy.Channel}/{policy.Name}";

        await dbContext.DeliveryChannelPolicies.AddAsync(policy);
        await dbContext.SaveChangesAsync();
        
        // Act
        var content = new StringContent(putDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var foundPolicy = dbContext.DeliveryChannelPolicies.Single(s => 
            s.Customer == customerId && 
            s.Name == policy.Name);
        foundPolicy.DisplayName.Should().Be("My IIIF AV Policy 2 (modified)");
        foundPolicy.PolicyData.Should().Be("[\"audio-mp3-256\"]");
    }
    
        
    [Fact]
    public async Task Patch_DeliveryChannelPolicy_201()
    {
        // Arrange
        const int customerId = 102;
        const string patchDeliveryChannelPolicyJson = @"{
            ""displayName"": ""My IIIF AV Policy 3 (modified)"",
            ""policyData"": ""[\""audio-mp3-256\""]""
        }";
        
        var policy = new DLCS.Model.Policies.DeliveryChannelPolicy()
        {
            Customer = customerId,
            Name = "put-av-policy-3",
            DisplayName = "My IIIF-AV Policy 3",
            Channel = "iiif-av",
            PolicyData = "[\"audio-mp3-128\"]"
        };
        
        var path = $"customers/{customerId}/deliveryChannelPolicies/{policy.Channel}/{policy.Name}";

        await dbContext.DeliveryChannelPolicies.AddAsync(policy);
        await dbContext.SaveChangesAsync();
        
        // Act
        var content = new StringContent(patchDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PatchAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var foundPolicy = dbContext.DeliveryChannelPolicies.Single(s => 
            s.Customer == customerId && 
            s.Name == policy.Name);
        foundPolicy.DisplayName.Should().Be("My IIIF AV Policy 3 (modified)");
        foundPolicy.PolicyData.Should().Be("[\"audio-mp3-256\"]");
    }
    
    [Fact]
    public async Task Delete_DeliveryChannelPolicy_204()
    {
        // Arrange
        const int customerId = 102;
        
        var policy = new DLCS.Model.Policies.DeliveryChannelPolicy()
        {
            Customer = customerId,
            Name = "delete-thumbs-policy",
            DisplayName = "My Thumbs Policy",
            Channel = "thumbs",
            PolicyData = "[\"!100,100\"]",
        };
        var path = $"customers/{customerId}/deliveryChannelPolicies/{policy.Channel}/{policy.Name}";

        await dbContext.DeliveryChannelPolicies.AddAsync(policy);
        await dbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.AsCustomer(customerId).DeleteAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var strategyExists = dbContext.DeliveryChannelPolicies.Any(p => p.Name == policy.Name);
        strategyExists.Should().BeFalse();
    }
}