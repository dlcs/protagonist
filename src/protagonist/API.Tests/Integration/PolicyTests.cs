using System.Net;
using System.Net.Http;
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
public class PolicyTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsContext dbContext;
    private readonly HttpClient httpClient;

    public PolicyTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
    {
        dbContext = dbFixture.DbContext;
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(dbFixture, "API-Test");
        dbFixture.CleanUp();
    }
    
    [Fact]
    public async Task Get_ImageOptimisationPolicies_200()
    {
        // Arrange
        var path = "imageOptimisationPolicies";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var model = await response.ReadAsHydraResponseAsync<HydraCollection<ImageOptimisationPolicy>>();
        model.Members.Should().HaveCount(3);
    }
    
    [Fact]
    public async Task Get_ImageOptimisationPolicies_SupportsPaging()
    {
        // Arrange
        var path = "imageOptimisationPolicies?pageSize=2";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var model = await response.ReadAsHydraResponseAsync<HydraCollection<ImageOptimisationPolicy>>();
        model.Members.Should().HaveCount(2);
    }
    
    [Fact]
    public async Task Get_ImageOptimisationPolicies_ReturnsCorrectIds()
    {
        // Arrange
        const int customerId = 100;
        var path = $"customers/{customerId}/imageOptimisationPolicies";
        var globalExample = new DLCS.Model.Policies.ImageOptimisationPolicy()
        {
            Customer = customerId,
            Id = "global-policy-example",
            Name = "Customer Specific Policy",
            TechnicalDetails = new[] { "Nothing yet" },
            Global = true
        };
        var customerSpecificExample = new DLCS.Model.Policies.ImageOptimisationPolicy()
        {
            Customer = customerId,
            Id = "customer-specific-policy-example",
            Name = "Global Policy",
            TechnicalDetails = new[] { "Nothing yet" },
            Global = false
        };
        
        await dbContext.ImageOptimisationPolicies.AddRangeAsync(globalExample, customerSpecificExample);
        await dbContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var model = await response.ReadAsHydraResponseAsync<HydraCollection<ImageOptimisationPolicy>>();

        model.Members.Should().ContainSingle(p => 
            p.Id == $"{httpClient.BaseAddress}imageOptimisationPolicies/{globalExample.Id}");
        model.Members.Should().ContainSingle(p => 
            p.Id == $"{httpClient.BaseAddress}customers/{customerId}/imageOptimisationPolicies/{customerSpecificExample.Id}");
    }
    
    [Fact]
    public async Task Get_ImageOptimisationPolicy_200()
    {
        // Arrange
        var path = "imageOptimisationPolicies/video-max";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var model = await response.ReadAsHydraResponseAsync<ImageOptimisationPolicy>();
        model.TechnicalDetails.Should().BeEquivalentTo("System preset: Webm 720p(webm)");
    }

    [Fact]
    public async Task Get_ImageOptimisationPolicy_404_IfNotFound()
    {
        // Arrange
        var path = "imageOptimisationPolicies/foofoo";

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_ImageOptimisationPolicy_404_IfCustomerSpecific()
    {
        // Arrange
        var path = "imageOptimisationPolicies/cust-default";

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_StoragePolicies_200()
    {
        // Arrange
        var path = "storagePolicies";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var model = await response.ReadAsHydraResponseAsync<HydraCollection<StoragePolicy>>();
        model.Members.Should().HaveCount(3);
    }
    
    [Fact]
    public async Task Get_StoragePolicies_SupportsPaging()
    {
        // Arrange
        var path = "storagePolicies?pageSize=1";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var model = await response.ReadAsHydraResponseAsync<HydraCollection<StoragePolicy>>();
        model.Members.Should().HaveCount(1);
    }
    
    [Fact]
    public async Task Get_StoragePolicy_200()
    {
        // Arrange
        var path = "storagePolicies/small";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var model = await response.ReadAsHydraResponseAsync<StoragePolicy>();
        model.MaximumNumberOfStoredImages.Should().Be(10);
        model.MaximumTotalSizeOfStoredImages.Should().Be(100);
    }

    [Fact]
    public async Task Get_StoragePolicy_400_IfNotFound()
    {
        // Arrange
        var path = "storagePolicies/foofoo";

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ThumbnailPolicies_200()
    {
        // Arrange
        var path = "thumbnailPolicies";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var model = await response.ReadAsHydraResponseAsync<HydraCollection<ThumbnailPolicy>>();
        model.Members.Should().HaveCount(1);
    }
    
    [Fact]
    public async Task Get_ThumbnailPolicy_200()
    {
        // Arrange
        var path = "thumbnailPolicies/default";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var model = await response.ReadAsHydraResponseAsync<ThumbnailPolicy>();
        model.Sizes.Should().BeEquivalentTo(new[] { 200, 400, 800 });
    }

    [Fact]
    public async Task Get_ThumbnailPolicy_400_IfNotFound()
    {
        // Arrange
        var path = "thumbnailPolicies/foofoo";

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_OriginStrategies_200()
    {
        // Arrange
        await dbContext.OriginStrategies.AddRangeAsync(
            new DLCS.Model.Policies.OriginStrategy { Id = "foo" },
            new DLCS.Model.Policies.OriginStrategy { Id = "bar" }
        );
        await dbContext.SaveChangesAsync();
        
        var path = "originStrategies";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var model = await response.ReadAsHydraResponseAsync<HydraCollection<OriginStrategy>>();
        model.Members.Should().HaveCount(2);
    }
    
    [Fact]
    public async Task Get_OriginStrategy_200()
    {
        // Arrange
        await dbContext.OriginStrategies.AddRangeAsync(
            new DLCS.Model.Policies.OriginStrategy { Id = "con" },
            new DLCS.Model.Policies.OriginStrategy { Id = "cept", RequiresCredentials = true}
        );
        await dbContext.SaveChangesAsync();
        var path = "originStrategies/cept";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var model = await response.ReadAsHydraResponseAsync<OriginStrategy>();
        model.RequiresCredentials.Should().BeTrue();
    }

    [Fact]
    public async Task Get_OriginStrategy_400_IfNotFound()
    {
        // Arrange
        var path = "originStrategies/foofoo";

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}