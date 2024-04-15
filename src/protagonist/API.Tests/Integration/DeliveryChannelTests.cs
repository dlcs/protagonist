using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.HydraModel;
using DLCS.Repository;
using DLCS.Repository.Messaging;
using FakeItEasy;
using Hydra.Collections;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Test.Helpers.Http;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class DeliveryChannelTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private static readonly IEngineClient EngineClient = A.Fake<IEngineClient>();
    private readonly ControllableHttpMessageHandler httpHandler;
    private readonly HttpClient httpClient;
    private readonly DlcsContext dbContext;
    private readonly string[] fakedAvPolicies =
    {
        "video-mp4-480p",
        "video-webm-720p",
        "audio-mp3-128k"
    };
    
    public DeliveryChannelTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
    {
        dbContext = dbFixture.DbContext;
        httpHandler = new ControllableHttpMessageHandler();
     
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(dbFixture, "API-Test",
            f => f.WithTestServices(services =>
            {
                services.AddAuthentication("API-Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("API-Test", _ => { });
                services.AddScoped<IEngineClient>(_ => EngineClient);
            }));
        
        A.CallTo(() => EngineClient.GetAllowedAvPolicyOptions(A<CancellationToken>._))
            .Returns(fakedAvPolicies);
        
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
    public async Task Post_DeliveryChannelPolicy_201_WithAvPolicy()
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
    
    [Theory]
    [MemberData(nameof(ValidThumbsPolicies))]
    public async Task Post_DeliveryChannelPolicy_201_WithThumbsPolicy(string policyName, string thumbParams)
    {
        // Arrange
        const int customerId = 88;
        var newDeliveryChannelPolicyJson = @$"{{
            ""name"": ""{policyName}"",
            ""displayName"": ""My Thumbs Policy"",
            ""policyData"": ""{thumbParams}""
        }}";
        
        var path = $"customers/{customerId}/deliveryChannelPolicies/thumbs";
   
        // Act
        var content = new StringContent(newDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var foundPolicy = dbContext.DeliveryChannelPolicies.Single(s => 
            s.Customer == customerId &&
            s.Name == policyName);
        foundPolicy.DisplayName.Should().Be("My Thumbs Policy");
       
        var expectedPolicyData = thumbParams.Replace(@"\", string.Empty);
        foundPolicy.PolicyData.Should().Be(expectedPolicyData);
    }
    
    [Fact]
    public async Task Post_DeliveryChannelPolicy_400_IfChannelInvalid()
    {
        // Arrange
        const int customerId = 88;
        const string newDeliveryChannelPolicyJson = @"{
            ""name"": ""post-invalid-policy"",
            ""displayName"": ""Invalid Policy"",
            ""policyData"": ""[\""audio-mp3-128k\""]""
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
            ""policyData"": ""[\""100,\""]""
        }";
        
        var path = $"customers/{customerId}/deliveryChannelPolicies/thumbs";
        var policy = new DLCS.Model.Policies.DeliveryChannelPolicy()
        {
            Customer = customerId,
            Name = "post-existing-policy",
            Channel = "thumbs",
            PolicyData = "[\"100,\"]"
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
            ""policyData"": ""[\""audio-mp3-128k\""]""
        }";
        
        var path = $"customers/{customerId}/deliveryChannelPolicies/iiif-av";

        // Act
        var content = new StringContent(newDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [MemberData(nameof(InvalidPutThumbsPolicies))]
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
    public async Task Post_DeliveryChannelPolicy_400_IfAvPolicyNonexistent()
    {
        // Arrange
        const int customerId = 88;
        
        var newDeliveryChannelPolicyJson = @"{{
            ""name"": ""post-invalid-iiif-av"",
            ""displayName"": ""Invalid Policy (IIIF-AV Policy Data)"",
            ""policyData"": ""[\""not-a-transcode-policy\""]""
        }}";
        var path = $"customers/{customerId}/deliveryChannelPolicies/iiif-av";

        // Act
        var content = new StringContent(newDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Post_DeliveryChannelPolicy_500_IfEngineAvPolicyEndpointUnreachable()
    {
        // Arrange
        const int customerId = 88;
        const string newDeliveryChannelPolicyJson = @"{
            ""name"": ""my-iiif-av-policy-1"",
            ""displayName"": ""My IIIF AV Policy"",
            ""policyData"": ""[\""video-mp4-480p\""]""
        }";
        
        var path = $"customers/{customerId}/deliveryChannelPolicies/iiif-av";
        
        A.CallTo(() => EngineClient.GetAllowedAvPolicyOptions(A<CancellationToken>._))
            .Returns((string[])null);
        
        // Act
        var content = new StringContent(newDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
    
    [Fact]
    public async Task Put_DeliveryChannelPolicy_200_WithAvPolicy()
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
    
    [Theory]
    [MemberData(nameof(ValidThumbsPolicies))]
    public async Task Put_DeliveryChannelPolicy_200_WithThumbsPolicy(string policyName, string thumbParams)
    {
        // Arrange
        const int customerId = 88;
        var putDeliveryChannelPolicyJson = @$"{{
            ""displayName"": ""My Thumbs Policy 2 (modified)"",
            ""policyData"": ""{thumbParams}""
        }}";
        
        var policy = new DLCS.Model.Policies.DeliveryChannelPolicy()
        {
            Customer = customerId,
            Name = policyName,
            DisplayName = "My Thumbs Policy 2",
            Channel = "thumbs",
            PolicyData = "[\"512,\"]"
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
        foundPolicy.DisplayName.Should().Be("My Thumbs Policy 2 (modified)");
        
        var expectedPolicyData = thumbParams.Replace(@"\", string.Empty);
        foundPolicy.PolicyData.Should().Be(expectedPolicyData);
    }
    
    [Fact]
    public async Task Put_DeliveryChannelPolicy_400_IfChannelInvalid()
    {
        // Arrange
        const int customerId = 88;
        const string newDeliveryChannelPolicyJson = @"{
            ""displayName"": ""Invalid Policy"",
            ""policyData"": ""[\""audio-mp3-128k\""]""
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
            ""policyData"": ""[\""audio-mp3-128k\""]""r
        }";
        
        var path = $"customers/{customerId}/deliveryChannelPolicies/iiif-av/FooBar";

        // Act
        var content = new StringContent(newDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [MemberData(nameof(InvalidPutThumbsPolicies))]
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
            PolicyData = "[\"100,\"]"
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
            PolicyData = "[\"100,\"]"
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
    public async Task Put_DeliveryChannelPolicy_500_IfEngineAvPolicyEndpointUnreachable()
    {
        // Arrange
        const int customerId = 88;
        const string putDeliveryChannelPolicyJson = @"{
            ""displayName"": ""My IIIF AV Policy 2 (modified)"",
            ""policyData"": ""[\""video-mp4-480p\""]""
        }";
        
        var path = $"customers/{customerId}/deliveryChannelPolicies/iiif-av/return-500-policy";
        
        A.CallTo(() => EngineClient.GetAllowedAvPolicyOptions(A<CancellationToken>._))
            .Returns((string[])null);
        
        // Act
        var content = new StringContent(putDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PatchAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
    
    [Fact]
    public async Task Patch_DeliveryChannelPolicy_201_WithAvPolicy()
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
    [MemberData(nameof(ValidThumbsPolicies))]
    public async Task Patch_DeliveryChannelPolicy_200_WithThumbsPolicy(string policyId, string policyData)
    {
        // Arrange
        const int customerId = 88;
        var patchDeliveryChannelPolicyJson = @$"{{
            ""displayName"": ""My Thumbs Policy 3 (modified)"",
            ""policyData"": ""{policyData}""
        }}";
        
        var policy = new DLCS.Model.Policies.DeliveryChannelPolicy()
        {
            Customer = customerId,
            Name = "put-thumbs-policy",
            DisplayName = "My Thumbs Policy 3",
            Channel = "thumbs",
            PolicyData = "[\"100,\"]"
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
        foundPolicy.DisplayName.Should().Be("My Thumbs Policy 3 (modified)");
        
        var expectedPolicyData = policyData.Replace(@"\", string.Empty);
        foundPolicy.PolicyData.Should().Be(expectedPolicyData);
    }
    
    [Theory]
    [MemberData(nameof(InvalidPatchThumbsPolicies))]
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
            PolicyData = "[\"100,\"]"
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
            PolicyData = "[\"100,\"]"
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
    public async Task Patch_DeliveryChannelPolicy_500_IfEngineAvPolicyEndpointUnreachable()
    {
        // Arrange
        const int customerId = 88;
        const string putDeliveryChannelPolicyJson = @"{
            ""displayName"": ""My IIIF AV Policy 2 (modified)"",
            ""policyData"": ""[\""video-mp4-480p\""]""
        }";
        
        var path = $"customers/{customerId}/deliveryChannelPolicies/iiif-av/return-500-policy";
        
        A.CallTo(() => EngineClient.GetAllowedAvPolicyOptions(A<CancellationToken>._))
            .Returns((string[])null);
        
        // Act
        var content = new StringContent(putDeliveryChannelPolicyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
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
    
    public static IEnumerable<object[]> ValidThumbsPolicies => new List<object[]>
    {
        new object[]
        {
            "my-thumbs-policy-1-a",
            @"[\""400,\"",\""200,\"",\""100,\""]"
        },
        new object[]
        {
            "my-thumbs-policy-1-b",
            @"[\""!400,\"",\""!200,\"",\""!100,\""]"
        },
        new object[]
        {
            "my-thumbs-policy-1-c",
            @"[\"",400\"",\"",200\"",\"",100\""]"
        },
        new object[]
        {
            "my-thumbs-policy-1-d",
            @"[\""!,400\"",\""!,200\"",\""!,100\""]"

        },
        new object[]
        {
            "my-thumbs-policy-1-e",
            @"[\""^400,\"",\""^200,\"",\""^100,\""]"
        },
        new object[]
        {
            "my-thumbs-policy-1-f",
            @"[\""^!400,\"",\""^!200,\"",\""^!100,\""]"
        },
        new object[]
        {
            "my-thumbs-policy-1-g",
            @"[\""^,400\"",\""^,200\"",\""^,100\""]"
        },
        new object[]
        {
            "my-thumbs-policy-1-h",
            @"[\""^!,400\"",\""^!,200\"",\""^!,100\""]"
        },
        new object[]
        {
            "my-thumbs-policy-1-i",
            @"[\""!400,400\"",\""!200,200\"",\""!100,100\""]"
        }
    };

    public static ICollection<object[]> InvalidPatchThumbsPolicies => new List<string>()
    {
        "[]", // Empty array
        "[\"\"]", // Array containing an empty value
        @"[\""foo\"",\""bar\""]", // Invalid data
        @"[\""100,100\"",\""200,200\""]", // Invalid JSON
        @"[\""max\""]", // SizeParameter specific rules
        @"[\""^max\""]",
        @"[\""441.6,7.5\""]",
        @"[\""441.6,\""]",
        @"[\"",7.5\""]",
        @"[\""pct:441.6,7.5\""]",
        @"[\""^pct:41.6,7.5\""]",
        @"[\""10,50\""]",
        @"[\"",\""]"
    }.Select(p => new object[] { p }).ToList();

    public static ICollection<object[]> InvalidPutThumbsPolicies => InvalidPatchThumbsPolicies.Concat(new List<object[]>()
    {
        new object[]
        {
            "" // No PolicyData specified
        }
    }).ToList();
}