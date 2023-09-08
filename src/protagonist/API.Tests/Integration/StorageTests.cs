using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using API.Tests.Integration.Infrastructure;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Spaces;
using DLCS.Model.Storage;
using DLCS.Repository;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class StorageTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    private readonly DlcsContext dlcsContext;

    public StorageTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
    {
        dlcsContext = dbFixture.DbContext;
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(dbFixture, "API-Test");
        dbFixture.CleanUp();
    }
    
    [Fact]
    public async Task Get_CustomerStorage_200()
    {
        // Arrange
        const int customerId = 90;
        
        var path = $"customers/{customerId}/storage";
        var customerStorage = new CustomerStorage()
        {
            StoragePolicy = "default",
            Customer = customerId,
            NumberOfStoredImages = 5,
            TotalSizeOfStoredImages = 8863407,
            TotalSizeOfThumbnails = 1029624,
            Space = 0,
        };
        
        await dlcsContext.CustomerStorages.AddAsync(customerStorage);
        await dlcsContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
    
    [Fact]
    public async Task Get_CustomerStorage_400_IfNotFound()
    {
        // Arrange
        const int customerId = 91;
        
        var path = $"customers/{customerId}/storage";

        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_SpaceStorage_200()
    {
        // Arrange
        const int customerId = 92;
        const int spaceId = 1;
        
        var path = $"customers/{customerId}/spaces/{spaceId}/storage";
        var space = new Space()
        {
            Id = spaceId,
            Name = "test-space",
            Customer = customerId,
            Created = DateTime.MinValue.ToUniversalTime()
          
        };
        var spaceStorage = new CustomerStorage()
        {
            StoragePolicy = "default",
            Customer = customerId,
            NumberOfStoredImages = 5,
            TotalSizeOfStoredImages = 8863407,
            TotalSizeOfThumbnails = 1029624,
            Space = spaceId,
        };
        
        await dlcsContext.Spaces.AddAsync(space);
        await dlcsContext.CustomerStorages.AddAsync(spaceStorage);
        await dlcsContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
    
    [Fact]
    public async Task Get_SpaceStorage_400_IfNotFound()
    {
        // Arrange
        const int customerId = 93;
        var path = $"customers/{customerId}/spaces/256/storage";

        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ImageStorage_200()
    {
        // Arrange
        const int customerId = 94;
        const string imageId = "test-image";
        
        var path = $"customers/{customerId}/spaces/0/images/{imageId}/storage";
        var imageStorage = new ImageStorage()
        {
            Id = AssetId.FromString($"{customerId}/0/{imageId}"),
            Customer = customerId,
            Space = 0,
            ThumbnailSize = 168104,
            Size = 2605964,
            LastChecked = DateTime.MinValue.ToUniversalTime()
        };
        
        await dlcsContext.ImageStorages.AddAsync(imageStorage);
        await dlcsContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
    
    [Fact]
    public async Task Get_ImageStorage_400_IfNotFound()
    {
        // Arrange
        const int customerId = 95;
        var path = $"customers/{customerId}/spaces/0/images/256/storage";

        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}