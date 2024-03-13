using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
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
        model.PolicyData.Should().Be("[\"!1024,1024\",\"!400,400\",\"!200,200\",\"!100,100\"]");
    }
    
    [Fact]
    public async Task Get_DeliveryChannelPolicy_404_IfNotFound()
    {
        // Arrange
        var path = $"customers/99/deliveryChannelPolicies/thumbs/foo";

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
            ""policyData"": ""[\""video-mp4-480p\""]""
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
        foundPolicy.PolicyData.Should().Be("[\"video-mp4-480p\"]");
    }
    
    [Fact]
    public async Task Post_DeliveryChannelPolicy_400_IfChannelInvalid()
    {
        // Arrange
        const int customerId = 88;
        const string newDeliveryChannelPolicyJson = @"{
            ""name"": ""post-invalid-policy"",
            ""displayName"": ""Invalid Policy"",
            ""policyData"": ""[\""audio-mp3-128\""]""
        }";
        
        var path = $"customers/{customerId}/deliveryChannelPolicies/foo";

        // Act
        var content = new StringContent(newDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Post_DeliveryChannelPolicy_409_IfNameTaken()
    {
        // Arrange
        const int customerId = 88;
        const string newDeliveryChannelPolicyJson = @"{
            ""name"": ""post-existing-policy"",
            ""policyData"": ""[\""100,100\""]""
        }";
        
        var path = $"customers/{customerId}/deliveryChannelPolicies/thumbs";
        var policy = new DLCS.Model.Policies.DeliveryChannelPolicy()
        {
            Customer = customerId,
            Name = "post-existing-policy",
            Channel = "thumbs",
            PolicyData = "[\"100,100\"]"
        };
        
        await dbContext.DeliveryChannelPolicies.AddAsync(policy);
        await dbContext.SaveChangesAsync();

        // Act
        var content = new StringContent(newDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
    
    [Fact]
    public async Task Post_DeliveryChannelPolicy_400_IfNameInvalid()
    {
        // Arrange
        const int customerId = 88;
        const string newDeliveryChannelPolicyJson = @"{
            ""name"": ""foo bar"",
            ""displayName"": ""Invalid Policy"",
            ""policyData"": ""[\""not-a-transcode-policy\""]""
        }";
        
        var path = $"customers/{customerId}/deliveryChannelPolicies/iiif-av";

        // Act
        var content = new StringContent(newDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [InlineData("")] // No PolicyData specified
    [InlineData("[]")] // Empty array
    [InlineData("[\"\"]")] // Array containing an empty value
    [InlineData(@"[\""foo\"",\""bar\""]")] // Invalid data
    [InlineData(@"[\""100,100\"",\""200,200\""")]  // Invalid JSON
    public async Task Post_DeliveryChannelPolicy_400_IfThumbsPolicyDataInvalid(string policyData)
    {
        // Arrange
        const int customerId = 88;
        
        var newDeliveryChannelPolicyJson = $@"{{
            ""name"": ""post-invalid-thumbs"",
            ""displayName"": ""Invalid Policy (Thumbs Policy Data)"",
            ""policyData"": ""{policyData}""
        }}";
        var path = $"customers/{customerId}/deliveryChannelPolicies/thumbs";

        // Act
        var content = new StringContent(newDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [InlineData("")] // No PolicyData specified
    [InlineData("[]")] // Empty array
    [InlineData("[\"\"]")] // Array containing an empty value
    [InlineData(@"[\""transcode-policy-1\"",\""\""]")] // Invalid data
    [InlineData(@"[\""transcode-policy\""")] // Invalid JSON
    public async Task Post_DeliveryChannelPolicy_400_IfAvPolicyDataInvalid(string policyData)
    {
        // Arrange
        const int customerId = 88;
        
        var newDeliveryChannelPolicyJson = $@"{{
            ""name"": ""post-invalid-iiif-av"",
            ""displayName"": ""Invalid Policy (IIIF-AV Policy Data)"",
            ""policyData"": ""{policyData}""
        }}";
        var path = $"customers/{customerId}/deliveryChannelPolicies/iiif-av";

        // Act
        var content = new StringContent(newDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Put_DeliveryChannelPolicy_200()
    {
        // Arrange
        const int customerId = 88;
        const string putDeliveryChannelPolicyJson = @"{
            ""displayName"": ""My IIIF AV Policy 2 (modified)"",
            ""policyData"": ""[\""video-mp4-480p\""]""
        }";
        
        var policy = new DLCS.Model.Policies.DeliveryChannelPolicy()
        {
            Customer = customerId,
            Name = "put-av-policy-2",
            DisplayName = "My IIIF-AV Policy 2",
            Channel = "iiif-av",
            PolicyData = "[\"video-webm-720p\"]"
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
        foundPolicy.PolicyData.Should().Be("[\"video-mp4-480p\"]");
    }
    
    [Fact]
    public async Task Put_DeliveryChannelPolicy_400_IfChannelInvalid()
    {
        // Arrange
        const int customerId = 88;
        const string newDeliveryChannelPolicyJson = @"{
            ""displayName"": ""Invalid Policy"",
            ""policyData"": ""[\""audio-mp3-128\""]""
        }";
        
        var path = $"customers/{customerId}/deliveryChannelPolicies/foo/put-invalid-channel-policy";

        // Act
        var content = new StringContent(newDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Put_DeliveryChannelPolicy_400_IfNameInvalid()
    {
        // Arrange
        const int customerId = 88;
        const string newDeliveryChannelPolicyJson = @"{
            ""displayName"": ""Invalid Policy"",
            ""policyData"": ""[\""audio-mp3-128\""]""r
        }";
        
        var path = $"customers/{customerId}/deliveryChannelPolicies/iiif-av/FooBar";

        // Act
        var content = new StringContent(newDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [InlineData("")] // No PolicyData specified
    [InlineData("[]")] // Empty array
    [InlineData("[\"\"]")] // Array containing an empty value
    [InlineData(@"[\""foo\"",\""bar\""]")] // Invalid data
    [InlineData(@"[\""100,100\"",\""200,200\""")]  // Invalid JSON
    public async Task Put_DeliveryChannelPolicy_400_IfThumbsPolicyDataInvalid(string policyData)
    {
        // Arrange
        const int customerId = 88;
        
        var newDeliveryChannelPolicyJson = $@"{{
            ""displayName"": ""Invalid Policy (Thumbs Policy Data)"",
            ""policyData"": ""{policyData}""
        }}";
        var path = $"customers/{customerId}/deliveryChannelPolicies/thumbs/put-invalid-thumbs";
        var policy = new DLCS.Model.Policies.DeliveryChannelPolicy()
        {
            Customer = customerId,
            Name = "put-invalid-thumbs",
            DisplayName = "Valid Policy (Thumbs Policy Data)",
            Channel = "thumbs",
            PolicyData = "[\"100,100\"]"
        };
        
        await dbContext.DeliveryChannelPolicies.AddAsync(policy);
        await dbContext.SaveChangesAsync();
        
        // Act
        var content = new StringContent(newDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [InlineData("")] // No PolicyData specified
    [InlineData("[]")] // Empty array
    [InlineData("[\"\"]")] // Array containing an empty value
    [InlineData(@"[\""transcode-policy-1\"",\""\""]")] // Invalid data
    [InlineData(@"[\""transcode-policy\""")] // Invalid JSON
    public async Task Put_DeliveryChannelPolicy_400_IfAvPolicyDataInvalid(string policyData)
    {
        // Arrange
        const int customerId = 88;
        
        var newDeliveryChannelPolicyJson = $@"{{
            ""displayName"": ""Invalid Policy (IIIF-AV Policy Data)"",
            ""policyData"": ""{policyData}""
        }}";
        var policy = new DLCS.Model.Policies.DeliveryChannelPolicy()
        {
            Customer = customerId,
            Name = "put-invalid-iiif-av",
            DisplayName = "Valid Policy (IIIF-AV Policy Data)",
            Channel = "thumbs",
            PolicyData = "[\"100,100\"]"
        };
        var path = $"customers/{customerId}/deliveryChannelPolicies/iiif-av/put-invalid-iiif-av";

        await dbContext.DeliveryChannelPolicies.AddAsync(policy);
        await dbContext.SaveChangesAsync();
        
        // Act
        var content = new StringContent(newDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Patch_DeliveryChannelPolicy_201()
    {
        // Arrange
        const int customerId = 88;
        const string patchDeliveryChannelPolicyJson = @"{
            ""displayName"": ""My IIIF AV Policy 3 (modified)"",
            ""policyData"": ""[\""video-webm-720p\""]""
        }";
        
        var policy = new DLCS.Model.Policies.DeliveryChannelPolicy()
        {
            Customer = customerId,
            Name = "put-av-policy",
            DisplayName = "My IIIF-AV Policy 3",
            Channel = "iiif-av",
            PolicyData = "[\"video-mp4-480p\"]"
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
        foundPolicy.PolicyData.Should().Be("[\"video-webm-720p\"]");
    }
    
    [Theory]
    [InlineData("[]")] // Empty array
    [InlineData("[\"\"]")] // Array containing an empty value
    [InlineData(@"[\""foo\"",\""bar\""]")] // Invalid data
    [InlineData(@"[\""100,100\"",\""200,200\""")]  // Invalid JSON
    public async Task Patch_DeliveryChannelPolicy_400_IfThumbsPolicyDataInvalid(string policyData)
    {
        // Arrange
        const int customerId = 88;
        
        var newDeliveryChannelPolicyJson = $@"{{
            ""policyData"": ""{policyData}""
        }}";
        var path = $"customers/{customerId}/deliveryChannelPolicies/thumbs/patch-invalid-thumbs";
        var policy = new DLCS.Model.Policies.DeliveryChannelPolicy()
        {
            Customer = customerId,
            Name = "patch-invalid-thumbs",
            DisplayName = "Valid Policy (Thumbs Policy Data)",
            Channel = "thumbs",
            PolicyData = "[\"100,100\"]"
        };
        
        await dbContext.DeliveryChannelPolicies.AddAsync(policy);
        await dbContext.SaveChangesAsync();
        
        // Act
        var content = new StringContent(newDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PatchAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [InlineData("[]")] // Empty array
    [InlineData("[\"\"]")] // Array containing an empty value
    [InlineData(@"[\""transcode-policy-1\"",\""\""]")] // Invalid data
    [InlineData(@"[\""transcode-policy\""")] // Invalid JSON
    public async Task Patch_DeliveryChannelPolicy_400_IfAvPolicyDataInvalid(string policyData)
    {
        // Arrange
        const int customerId = 88;
        
        var newDeliveryChannelPolicyJson = $@"{{
            ""policyData"": ""{policyData}""
        }}";
        var policy = new DLCS.Model.Policies.DeliveryChannelPolicy()
        {
            Customer = customerId,
            Name = "patch-invalid-iiif-av",
            DisplayName = "Valid Policy (IIIF-AV Policy Data)",
            Channel = "iiif-av",
            PolicyData = "[\"100,100\"]"
        };
        var path = $"customers/{customerId}/deliveryChannelPolicies/iiif-av/patch-invalid-iiif-av";

        await dbContext.DeliveryChannelPolicies.AddAsync(policy);
        await dbContext.SaveChangesAsync();
        
        // Act
        var content = new StringContent(newDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PatchAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Delete_DeliveryChannelPolicy_204()
    {
        // Arrange
        const int customerId = 88;
        
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

        var policyExists = dbContext.DeliveryChannelPolicies.Any(p => p.Name == policy.Name);
        policyExists.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_DeliveryChannelPolicy_404_IfNotFound()
    {
        // Arrange
        const int customerId = 88;
        var path = $"customers/{customerId}/deliveryChannelPolicies/thumbs/foo";
        
        // Act
        var response = await httpClient.AsCustomer(customerId).DeleteAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_DeliveryChannelPolicyCollections_200()
    {
        // Arrange
        const int customerId = 88;

        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync($"customers/{customerId}/deliveryChannelPolicies");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var collections = await response.ReadAsHydraResponseAsync<HydraCollection<HydraNestedCollection<DeliveryChannelPolicy>>>();
        collections.TotalItems.Should().Be(4); // Should contain iiif-img, thumbs, iiif-av and file 
    }
    
    [Fact]
    public async Task Get_DeliveryChannelPolicyCollection_200()
    {
        // Arrange
        const int customerId = 99;
     
        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync($"customers/{customerId}/deliveryChannelPolicies/thumbs");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var collection = await response.ReadAsHydraResponseAsync<HydraCollection<DeliveryChannelPolicy>>();
        collection.TotalItems.Should().Be(1);
        
        var createdPolicy = collection.Members.FirstOrDefault();
        createdPolicy.Name.Should().Be("example-thumbs-policy");
        createdPolicy.Channel.Should().Be("thumbs");
        createdPolicy.PolicyData.Should().Be("[\"!1024,1024\",\"!400,400\",\"!200,200\",\"!100,100\"]");
    }
    
    [Fact]
    public async Task Get_DeliveryChannelPolicyCollection_400_IfChannelInvalid()
    {
        // Arrange
        const int customerId = 88;

        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync($"customers/{customerId}/deliveryChannelPolicies/foo");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}