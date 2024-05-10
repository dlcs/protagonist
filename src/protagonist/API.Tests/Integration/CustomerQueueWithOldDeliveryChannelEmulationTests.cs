using System.Net;
using System.Net.Http;
using System.Text;
using API.Tests.Integration.Infrastructure;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Repository.Messaging;
using FakeItEasy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class CustomerQueueWithOldDeliveryChannelEmulationTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsContext dbContext;
    private readonly HttpClient httpClient;
    private static readonly IEngineClient EngineClient = A.Fake<IEngineClient>();

    public CustomerQueueWithOldDeliveryChannelEmulationTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
    {
        dbContext = dbFixture.DbContext;
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithConfigValue("DeliveryChannelsEnabled", "true")
            .WithConfigValue("EmulateOldDeliveryChannelProperties", "true")
            .WithTestServices(services =>
            {
                services.AddScoped<IEngineClient>(_ => EngineClient);
                services.AddAuthentication("API-Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        "API-Test", _ => { });
            })
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        dbFixture.CleanUp();
    }
    
    [Fact]
    public async Task Post_CreateBatch_201_SupportsOldDeliveryChannelEmulation_ForImageChannel()
    {
        const int customerId = 99;
        const int space = 1;
        
        // Arrange
        var hydraImageBody = $@"{{
            ""@context"": ""http://www.w3.org/ns/hydra/context.jsonld"",
            ""@type"": ""Collection"",
            ""member"": [
                {{
                  ""id"": ""{nameof(Post_CreateBatch_201_SupportsOldDeliveryChannelEmulation_ForImageChannel)}"",
                  ""origin"": ""https://example.org/img.tiff"",
                  ""space"": 1,
                  ""family"": ""I"",
                  ""mediaType"": ""image/tiff"",
                  ""wcDeliveryChannels"": [""iiif-img""]
                }}
            ]
        }}";

        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var path = $"/customers/{customerId}/queue";

        // Act
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var assetInDatabase = await dbContext.Images
            .IncludeDeliveryChannelsWithPolicy()
            .SingleAsync(a => a.Customer == customerId && a.Space == space);
        
        assetInDatabase.ImageDeliveryChannels.Should().Satisfy(
            dc => dc.Channel == "iiif-img" && dc.DeliveryChannelPolicy.Name == "default",
                  dc => dc.Channel == "thumbs" && dc.DeliveryChannelPolicy.Name == "default");
    }
    
    [Fact]
    public async Task Post_CreateBatch_201_SupportsOldDeliveryChannelEmulation_ForImageChannel_WithUseOriginalPolicy()
    {
        const int customerId = 99;
        const int space = 1;
        
        // Arrange
        var hydraImageBody = $@"{{
            ""@context"": ""http://www.w3.org/ns/hydra/context.jsonld"",
            ""@type"": ""Collection"",
            ""member"": [
                {{
                  ""id"": ""{nameof(Post_CreateBatch_201_SupportsOldDeliveryChannelEmulation_ForImageChannel_WithUseOriginalPolicy)}"",
                  ""origin"": ""https://example.org/img.tiff"",
                  ""space"": 1,
                  ""family"": ""I"",
                  ""mediaType"": ""image/tiff"",
                  ""imageOptimisationPolicy"": ""use-original"",
                  ""wcDeliveryChannels"": [""iiif-img""]
                }}
            ]
        }}";

        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var path = $"/customers/{customerId}/queue";

        // Act
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var assetInDatabase = await dbContext.Images
            .IncludeDeliveryChannelsWithPolicy()
            .SingleAsync(a => a.Customer == customerId && a.Space == space);
        
        assetInDatabase.ImageDeliveryChannels.Should().Satisfy(
            dc => dc.Channel == "iiif-img" && dc.DeliveryChannelPolicy.Name == "use-original",
            dc => dc.Channel == "thumbs" && dc.DeliveryChannelPolicy.Name == "default");
    }
    
    [Fact]
    public async Task Post_CreateBatch_201_SupportsOldDeliveryChannelEmulation_ForAvChannel_Video()
    {
        const int customerId = 99;
        const int space = 1;
        
        // Arrange
        var hydraImageBody = $@"{{
            ""@context"": ""http://www.w3.org/ns/hydra/context.jsonld"",
            ""@type"": ""Collection"",
            ""member"": [
                {{
                  ""id"": ""{nameof(Post_CreateBatch_201_SupportsOldDeliveryChannelEmulation_ForAvChannel_Video)}"",
                  ""origin"": ""https://example.org/video.mp4"",
                  ""space"": 1,
                  ""family"": ""T"",
                  ""mediaType"": ""video/mp4"",
                  ""wcDeliveryChannels"": [""iiif-av""]
                }}
            ]
        }}";

        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var path = $"/customers/{customerId}/queue";

        // Act
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var assetInDatabase = await dbContext.Images
            .IncludeDeliveryChannelsWithPolicy()
            .SingleAsync(a => a.Customer == customerId && a.Space == space);
        
        assetInDatabase.ImageDeliveryChannels.Should().Satisfy(
            dc => dc.Channel == "iiif-av" &&
                  dc.DeliveryChannelPolicy.Name == "default-video");
    }
    
    [Fact]
    public async Task Post_CreateBatch_201_SupportsOldDeliveryChannelEmulation_ForAvChannel_Audio()
    {
        const int customerId = 99;
        const int space = 1;
        
        // Arrange
        var hydraImageBody = $@"{{
            ""@context"": ""http://www.w3.org/ns/hydra/context.jsonld"",
            ""@type"": ""Collection"",
            ""member"": [
                {{
                  ""id"": ""{nameof(Post_CreateBatch_201_SupportsOldDeliveryChannelEmulation_ForAvChannel_Audio)}"",
                  ""origin"": ""https://example.org/audio.mp3"",
                  ""space"": 1,
                  ""family"": ""T"",
                  ""mediaType"": ""audio/mp3"",
                  ""wcDeliveryChannels"": [""iiif-av""]
                }}
            ]
        }}";

        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var path = $"/customers/{customerId}/queue";

        // Act
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var assetInDatabase = await dbContext.Images
            .IncludeDeliveryChannelsWithPolicy()
            .SingleAsync(a => a.Customer == customerId && a.Space == space);
        
        assetInDatabase.ImageDeliveryChannels.Should().Satisfy(
            dc => dc.Channel == "iiif-av" &&
                  dc.DeliveryChannelPolicy.Name == "default-audio");
    }
    
    [Fact]
    public async Task Post_CreateBatch_201_SupportsOldDeliveryChannelEmulation_ForFileChannel()
    {
        const int customerId = 99;
        const int space = 1;
        
        // Arrange
        var hydraImageBody = $@"{{
            ""@context"": ""http://www.w3.org/ns/hydra/context.jsonld"",
            ""@type"": ""Collection"",
            ""member"": [
                {{
                  ""id"": ""{nameof(Post_CreateBatch_201_SupportsOldDeliveryChannelEmulation_ForFileChannel)}"",
                  ""origin"": ""https://example.org/vid.mp4"",
                  ""space"": 1,
                  ""family"": ""T"",
                  ""mediaType"": ""video/mp4"",
                  ""wcDeliveryChannels"": [""file""]
                }}
            ]
        }}";

        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var path = $"/customers/{customerId}/queue";

        // Act
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var assetInDatabase = await dbContext.Images
            .IncludeDeliveryChannelsWithPolicy()
            .SingleAsync(a => a.Customer == customerId && a.Space == space);
        
        assetInDatabase.ImageDeliveryChannels.Should().Satisfy(
            dc => dc.Channel == "file" &&
                  dc.DeliveryChannelPolicy.Name == "none");
    }
    
    [Fact]
    public async Task Post_CreateBatch_201_SupportsOldDeliveryChannelEmulation_ForMultipleChannels()
    {
        const int customerId = 99;
        const int space = 1;
        
        // Arrange
        var hydraImageBody = $@"{{
            ""@context"": ""http://www.w3.org/ns/hydra/context.jsonld"",
            ""@type"": ""Collection"",
            ""member"": [
                {{
                  ""id"": ""{nameof(Post_CreateBatch_201_SupportsOldDeliveryChannelEmulation_ForMultipleChannels)}"",
                  ""origin"": ""https://example.org/img.tiff"",
                  ""space"": 1,
                  ""family"": ""I"",
                  ""mediaType"": ""image/tiff"",
                  ""wcDeliveryChannels"": [""iiif-img"",""file""]
                }}
            ]
        }}";

        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var path = $"/customers/{customerId}/queue";

        // Act
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var assetInDatabase = await dbContext.Images
            .IncludeDeliveryChannelsWithPolicy()
            .SingleAsync(a => a.Customer == customerId && a.Space == space);
        
        assetInDatabase.ImageDeliveryChannels.Should().Satisfy(
            dc => dc.Channel == "iiif-img" &&
                  dc.DeliveryChannelPolicy.Name == "default",
            dc => dc.Channel == "thumbs" && 
                  dc.DeliveryChannelPolicy.Name == "default",
            dc => dc.Channel == "file" && 
                  dc.DeliveryChannelPolicy.Name == "none");
    }
    
    [Fact]
    public async Task Post_CreateBatch_400_IfWcDeliveryChannelInvalid()
    {
        const int customerId = 99;
        
        // Arrange
        var hydraImageBody = $@"{{
            ""@context"": ""http://www.w3.org/ns/hydra/context.jsonld"",
            ""@type"": ""Collection"",
            ""member"": [
                {{
                  ""id"": ""{nameof(Post_CreateBatch_201_SupportsOldDeliveryChannelEmulation_ForFileChannel)}"",
                  ""origin"": ""https://example.org/vid.mp4"",
                  ""space"": 1,
                  ""family"": ""T"",
                  ""mediaType"": ""video/mp4"",
                  ""wcDeliveryChannels"": [""not-a-channel""]
                }}
            ]
        }}";

        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var path = $"/customers/{customerId}/queue";

        // Act
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}