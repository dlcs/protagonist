using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using API.Infrastructure.Messaging;
using API.Tests.Integration.Infrastructure;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using DLCS.Repository;
using DLCS.Repository.Messaging;
using FakeItEasy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;
using AssetFamily = DLCS.HydraModel.AssetFamily;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(StorageCollection.CollectionName)]
public class ModifyAssetWithOldDeliveryChannelEmulationTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsContext dbContext;
    private readonly HttpClient httpClient;
    private static readonly IAssetNotificationSender NotificationSender = A.Fake<IAssetNotificationSender>();
    private static readonly IEngineClient EngineClient = A.Fake<IEngineClient>();
    
    public ModifyAssetWithOldDeliveryChannelEmulationTests(
        StorageFixture storageFixture, 
        ProtagonistAppFactory<Startup> factory)
    {
        var dbFixture = storageFixture.DbFixture;
        dbContext = dbFixture.DbContext;
        
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithLocalStack(storageFixture.LocalStackFixture)
            .WithTestServices(services =>
            {
                services.AddSingleton<IAssetNotificationSender>(_ => NotificationSender);
                services.AddScoped<IEngineClient>(_ => EngineClient);
                services.AddAuthentication("API-Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        "API-Test", _ => { });
            })
            .WithConfigValue("DeliveryChannelsEnabled", "true")
            .WithConfigValue("EmulateOldDeliveryChannelProperties", "true")
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        
        dbFixture.CleanUp();
    }
    
    [Theory]
    [InlineData(AssetFamily.File, "application/pdf", "pdf", false)]
    [InlineData(AssetFamily.Image, "image/tiff", "tiff", false)]
    [InlineData(AssetFamily.Timebased, "video/mp4", "mp4", true)] 
    public async Task Put_New_Asset_Translates_FileWcDeliveryChannel(AssetFamily assetFamily, string mediaType, 
        string fileExtension, bool isIngestedAsync)
    {
        var assetId = new AssetId(99, 1, $"{nameof(Put_New_Asset_Translates_FileWcDeliveryChannel)}-${fileExtension}");
        var hydraImageBody = $@"{{
          ""@type"": ""Image"",
          ""origin"": ""https://example.org/{assetId.Asset}.{fileExtension}"",
          ""family"": ""{assetFamily}"",
          ""mediaType"": ""{mediaType}"",
          ""wcDeliveryChannels"": [""file""]
        }}";

        if (isIngestedAsync)
        {
            A.CallTo(() =>
                    EngineClient.AsynchronousIngest(
                        A<Asset>.That.Matches(r => r.Id == assetId),
                        A<CancellationToken>._))
                .Returns(true);
        }
        else
        {
            A.CallTo(() =>
                    EngineClient.SynchronousIngest(
                        A<Asset>.That.Matches(r => r.Id == assetId),
                        A<CancellationToken>._))
                .Returns(HttpStatusCode.OK); 
        }
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
      
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(dc => dc.DeliveryChannelPolicy).Single(x => x.Id == assetId);
        asset.ImageDeliveryChannels.Should().Satisfy(
            dc => dc.Channel == "file" &&
                  dc.DeliveryChannelPolicy.Name == "none");
    }
    
    [Fact]
    public async Task Put_New_Asset_Translates_MultipleWcDeliveryChannels()
    {
        var assetId = new AssetId(99, 1, nameof(Put_New_Asset_Translates_MultipleWcDeliveryChannels));
        var hydraImageBody = $@"{{
          ""@type"": ""Image"",
          ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
          ""family"": ""I"",
          ""mediaType"": ""image/tiff"",
          ""wcDeliveryChannels"": [""iiif-img"",""thumbs"",""file""]
        }}";

        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(dc => dc.DeliveryChannelPolicy).Single(x => x.Id == assetId);
        asset.ImageDeliveryChannels.Should().Satisfy(
            dc => dc.Channel == "iiif-img" &&
                  dc.DeliveryChannelPolicy.Name == "default",
            dc => dc.Channel == "thumbs" && 
                  dc.DeliveryChannelPolicy.Name == "default",
            dc => dc.Channel == "file" && 
                  dc.DeliveryChannelPolicy.Name == "none");
    }
    
    [Fact]
    public async Task Put_New_Asset_Translates_ImageWcDeliveryChannel()
    {
        var assetId = new AssetId(99, 1, nameof(Put_New_Asset_Translates_ImageWcDeliveryChannel));
        var hydraImageBody = $@"{{
          ""@type"": ""Image"",
          ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
          ""family"": ""I"",
          ""mediaType"": ""image/tiff"",
          ""wcDeliveryChannels"": [""iiif-img""]
        }}";

        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(dc => dc.DeliveryChannelPolicy).Single(x => x.Id == assetId);
        asset.ImageDeliveryChannels.Should().Satisfy(
            dc => dc.Channel == "iiif-img" && dc.DeliveryChannelPolicy.Name == "default",
            dc => dc.Channel == "thumbs" && dc.DeliveryChannelPolicy.Name == "default");
    }
    
    [Fact]
    public async Task Put_New_Asset_Translates_ImageWcDeliveryChannel_WithUseOriginalPolicy()
    {
        var assetId = new AssetId(99, 1, nameof(Put_New_Asset_Translates_ImageWcDeliveryChannel_WithUseOriginalPolicy));
        var hydraImageBody = $@"{{
          ""@type"": ""Image"",
          ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
          ""family"": ""I"",
          ""mediaType"": ""image/tiff"",
          ""imageOptimisationPolicy"": ""use-original"",
          ""wcDeliveryChannels"": [""iiif-img""]
        }}";

        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(dc => dc.DeliveryChannelPolicy).Single(x => x.Id == assetId);
        asset.ImageDeliveryChannels.Should().Satisfy(
            dc => dc.Channel == "iiif-img" && dc.DeliveryChannelPolicy.Name == "use-original",
            dc => dc.Channel == "thumbs" && dc.DeliveryChannelPolicy.Name == "default");
    }
    
    [Fact]
    public async Task Put_New_Asset_Translates_AvWcDeliveryChannel_Video()
    {
        var assetId = new AssetId(99, 1, nameof(Put_New_Asset_Translates_AvWcDeliveryChannel_Video));
        var hydraImageBody = $@"{{
          ""@type"": ""Image"",
          ""origin"": ""https://example.org/{assetId.Asset}.mp4"",
          ""family"": ""T"",
          ""mediaType"": ""video/mp4"",
          ""wcDeliveryChannels"": [""iiif-av""]
        }}";
        
        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(true);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(dc => dc.DeliveryChannelPolicy).Single(x => x.Id == assetId);
        asset.ImageDeliveryChannels.Should().Satisfy(
            dc => dc.Channel == "iiif-av" &&
                  dc.DeliveryChannelPolicy.Name == "default-video");
    }
    
    [Fact]
    public async Task Put_New_Asset_Translates_AvWcDeliveryChannel_Audio()
    {
        var assetId = new AssetId(99, 1, nameof(Put_New_Asset_Translates_AvWcDeliveryChannel_Audio));
        var hydraImageBody = $@"{{
          ""@type"": ""Image"",
          ""origin"": ""https://example.org/{assetId.Asset}.mp3"",
          ""family"": ""T"",
          ""mediaType"": ""audio/mp3"",
          ""wcDeliveryChannels"": [""iiif-av""]
        }}";

        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(true);
            
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
     
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(dc => dc.DeliveryChannelPolicy).Single(x => x.Id == assetId);
        asset.ImageDeliveryChannels.Should().Satisfy(
            dc => dc.Channel == "iiif-av" &&
                  dc.DeliveryChannelPolicy.Name == "default-audio");
    }
    
    [Fact]
    public async Task Put_New_Asset_BadRequest_IfWcDeliveryChannelInvalid()
    {
        var assetId = new AssetId(99, 1, nameof(Put_New_Asset_BadRequest_IfWcDeliveryChannelInvalid));
        var hydraImageBody = $@"{{
          ""@type"": ""Image"",
          ""origin"": ""https://example.org/{assetId.Asset}.mp3"",
          ""family"": ""T"",
          ""mediaType"": ""audio/mp3"",
          ""wcDeliveryChannels"": [""not-a-channel""]
        }}";
        
        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(true);
            
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [InlineData(DLCS.Model.Assets.AssetFamily.File, "application/pdf", "pdf", false)]
    [InlineData(DLCS.Model.Assets.AssetFamily.Image, "image/tiff", "tiff", false)]
    [InlineData(DLCS.Model.Assets.AssetFamily.Timebased, "video/mp4", "mp4", true)] 
    public async Task Patch_Asset_Translates_FileWcDeliveryChannel(DLCS.Model.Assets.AssetFamily assetFamily, string mediaType, 
        string fileExtension, bool isIngestedAsync)
    {
        var assetId = new AssetId(99, 1, $"{nameof(Patch_Asset_Translates_FileWcDeliveryChannel)}-${fileExtension}");
        var hydraImageBody = @"{
          ""wcDeliveryChannels"": [""file""]
        }";
        
        await dbContext.Images.AddTestAsset(assetId, origin: $"https://example.org/{assetId.Asset}.{fileExtension}", 
            mediaType: mediaType, family: assetFamily);
        await dbContext.SaveChangesAsync();

        if (isIngestedAsync)
        {
            A.CallTo(() =>
                    EngineClient.AsynchronousIngest(
                        A<Asset>.That.Matches(r => r.Id == assetId),
                        A<CancellationToken>._))
                .Returns(true);
        }
        else
        {
            A.CallTo(() =>
                    EngineClient.SynchronousIngest(
                        A<Asset>.That.Matches(r => r.Id == assetId),
                        A<CancellationToken>._))
                .Returns(HttpStatusCode.OK); 
        }
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
      
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(dc => dc.DeliveryChannelPolicy).Single(x => x.Id == assetId);
        asset.ImageDeliveryChannels.Should().Satisfy(
            dc => dc.Channel == "file" &&
                  dc.DeliveryChannelPolicy.Name == "none");
    }
    
    [Fact]
    public async Task Patch_Asset_Translates_MultipleWcDeliveryChannels()
    {
        var assetId = new AssetId(99, 1, nameof(Patch_Asset_Translates_MultipleWcDeliveryChannels));
        var hydraImageBody = @"{
          ""wcDeliveryChannels"": [""iiif-img"",""thumbs"",""file""]
        }";

        await dbContext.Images.AddTestAsset(assetId, origin: $"https://example.org/{assetId.Asset}.tiff", 
            mediaType: "image/tiff", family: DLCS.Model.Assets.AssetFamily.Image);
        await dbContext.SaveChangesAsync();
        
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(dc => dc.DeliveryChannelPolicy).Single(x => x.Id == assetId);
        asset.ImageDeliveryChannels.Should().Satisfy(
            dc => dc.Channel == "iiif-img" &&
                  dc.DeliveryChannelPolicy.Name == "default",
            dc => dc.Channel == "thumbs" && 
                  dc.DeliveryChannelPolicy.Name == "default",
            dc => dc.Channel == "file" && 
                  dc.DeliveryChannelPolicy.Name == "none");
    }
    
    [Fact]
    public async Task Patch_Asset_Translates_ImageWcDeliveryChannel()
    {
        var assetId = new AssetId(99, 1, nameof(Patch_Asset_Translates_ImageWcDeliveryChannel));
        var hydraImageBody = @"{
          ""wcDeliveryChannels"": [""iiif-img""]
        }";

        await dbContext.Images.AddTestAsset(assetId, origin: $"https://example.org/{assetId.Asset}.tiff", 
            mediaType: "image/tiff", family: DLCS.Model.Assets.AssetFamily.Image);
        await dbContext.SaveChangesAsync();
        
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(dc => dc.DeliveryChannelPolicy).Single(x => x.Id == assetId);
        asset.ImageDeliveryChannels.Should().Satisfy(
            dc => dc.Channel == "iiif-img" && dc.DeliveryChannelPolicy.Name == "default",
            dc => dc.Channel == "thumbs" && dc.DeliveryChannelPolicy.Name == "default");
    }
    
    [Fact]
    public async Task Patch_Asset_Translates_ImageWcDeliveryChannel_WithUseOriginalPolicy()
    {
        var assetId = new AssetId(99, 1, nameof(Patch_Asset_Translates_ImageWcDeliveryChannel_WithUseOriginalPolicy));
        var hydraImageBody = @"{
          ""imageOptimisationPolicy"": ""use-original"",
          ""wcDeliveryChannels"": [""iiif-img""]
        }";

        await dbContext.Images.AddTestAsset(assetId, origin: $"https://example.org/{assetId.Asset}.tiff", 
            mediaType: "image/tiff", family: DLCS.Model.Assets.AssetFamily.Image);
        await dbContext.SaveChangesAsync();
        
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(dc => dc.DeliveryChannelPolicy).Single(x => x.Id == assetId);
        asset.ImageDeliveryChannels.Should().Satisfy(
            dc => dc.Channel == "iiif-img" && dc.DeliveryChannelPolicy.Name == "use-original",
            dc => dc.Channel == "thumbs" && dc.DeliveryChannelPolicy.Name == "default");
    }
    
    [Fact]
    public async Task Patch_Asset_Translates_AvWcDeliveryChannel_Video()
    {
        var assetId = new AssetId(99, 1, nameof(Patch_Asset_Translates_AvWcDeliveryChannel_Video));
        var hydraImageBody = $@"{{
          ""@type"": ""Image"",
          ""origin"": ""https://example.org/{assetId.Asset}.mp4"",
          ""family"": ""T"",
          ""mediaType"": ""video/mp4"",
          ""wcDeliveryChannels"": [""iiif-av""]
        }}";
        
        await dbContext.Images.AddTestAsset(assetId, origin: $"https://example.org/{assetId.Asset}.mp3", 
            mediaType: "video/mp4", family: DLCS.Model.Assets.AssetFamily.Timebased);
        await dbContext.SaveChangesAsync();
        
        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(true);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(dc => dc.DeliveryChannelPolicy).Single(x => x.Id == assetId);
        asset.ImageDeliveryChannels.Should().Satisfy(
            dc => dc.Channel == "iiif-av" &&
                  dc.DeliveryChannelPolicy.Name == "default-video");
    }
    
    [Fact]
    public async Task Patch_Asset_Translates_AvWcDeliveryChannel_Audio()
    {
        var assetId = new AssetId(99, 1, nameof(Patch_Asset_Translates_AvWcDeliveryChannel_Audio));
        var hydraImageBody = $@"{{
          ""wcDeliveryChannels"": [""iiif-av""]
        }}";
        
        await dbContext.Images.AddTestAsset(assetId, origin: $"https://example.org/{assetId.Asset}.mp3", 
            mediaType: "audio/mp3", family: DLCS.Model.Assets.AssetFamily.Timebased);
        await dbContext.SaveChangesAsync();
        
        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(true);
            
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(dc => dc.DeliveryChannelPolicy).Single(x => x.Id == assetId);
        asset.ImageDeliveryChannels.Should().Satisfy(
            dc => dc.Channel == "iiif-av" &&
                  dc.DeliveryChannelPolicy.Name == "default-audio");
    }
    
    [Fact]
    public async Task Patch_Asset_BadRequest_IfWcDeliveryChannelInvalid()
    {
        var assetId = new AssetId(99, 1, nameof(Patch_Asset_BadRequest_IfWcDeliveryChannelInvalid));
        var hydraImageBody = @"{
          ""wcDeliveryChannels"": [""not-a-channel""]
        }";
        
        await dbContext.Images.AddTestAsset(assetId, origin: $"https://example.org/{assetId.Asset}.mp3", 
            mediaType: "audio/mp3", family: DLCS.Model.Assets.AssetFamily.Timebased);
        await dbContext.SaveChangesAsync();
        
        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(true);
            
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Patch_Asset_Change_ImageOptimisationPolicy_Allowed()
    {
        var assetId = new AssetId(99, 1, nameof(Patch_Asset_Change_ImageOptimisationPolicy_Allowed));

        var asset = (await dbContext.Images.AddTestAsset(assetId, origin: "https://images.org/image1.tiff")).Entity;
        var testPolicy = new ImageOptimisationPolicy
        {
            Id = "test-policy",
            Name = "Test Policy",
            TechnicalDetails = new[] { "1010101" }
        };
        dbContext.ImageOptimisationPolicies.Add(testPolicy);
        await dbContext.SaveChangesAsync();
        
        var hydraImageBody = $@"{{
          ""@type"": ""Image"",
          ""imageOptimisationPolicy"": ""http://localhost/imageOptimisationPolicies/test-policy""
        }}";
        
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);

        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .MustHaveHappened();
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        await dbContext.Entry(asset).ReloadAsync();
        asset.ImageOptimisationPolicy.Should().Be("test-policy");
    }
}