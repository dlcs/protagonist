using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using Amazon.S3;
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
using AssetFamily = DLCS.Model.Assets.AssetFamily;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(StorageCollection.CollectionName)]
public class ModifyAssetWithOldDeliveryChannelPropertiesTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsContext dbContext;
    private readonly HttpClient httpClient;
    private static readonly IAssetNotificationSender NotificationSender = A.Fake<IAssetNotificationSender>();
    private static readonly IEngineClient EngineClient = A.Fake<IEngineClient>();

    public ModifyAssetWithOldDeliveryChannelPropertiesTests(
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
            .WithConfigValue("EmulateOldDeliveryChannelProperties", "true")
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        dbFixture.CleanUp();
    }
    
    [Theory]
    [InlineData("fast-higher")]
    [InlineData("https://api.dlc.services/imageOptimisationPolicies/fast-higher")]
    public async Task Put_NewImageAsset_WithImageOptimisationPolicy_Creates_Asset_WhenLegacyEnabled(string imageOptimisationPolicy)
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = new AssetId(customer, space, nameof(Put_NewImageAsset_WithImageOptimisationPolicy_Creates_Asset_WhenLegacyEnabled));
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Spaces.AddTestSpace(customer, space);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(customer);
        await dbContext.DeliveryChannelPolicies.AddTestDeliveryChannelPolicies(customer);
        await dbContext.SaveChangesAsync();

        var hydraImageBody = $@"{{
          ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
          ""imageOptimisationPolicy"" : ""{imageOptimisationPolicy}""
        }}";

        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);

        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customer).PutAsync(assetId.ToApiResourcePath(), content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(i => i.DeliveryChannelPolicy).Single(i => i.Id == assetId);
        asset.Id.Should().Be(assetId);
        asset.MediaType.Should().Be("image/tiff");
        asset.Family.Should().Be(AssetFamily.Image);
        asset.ImageOptimisationPolicy.Should().Be("fast-higher");
        asset.ThumbnailPolicy.Should().BeEmpty();
        asset.ImageDeliveryChannels.Count.Should().Be(2);
        asset.ImageDeliveryChannels.Should().ContainSingle(dc => dc.Channel == AssetDeliveryChannels.Image &&
                                                                dc.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ImageDefault);
        asset.ImageDeliveryChannels.Should().ContainSingle(dc => dc.Channel == AssetDeliveryChannels.Thumbnails &&
                                                                dc.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ThumbsDefault);
    }
    
    [Fact]
    public async Task Put_NewImageAsset_WithImageOptimisationPolicy_Returns400_IfInvalid_AndLegacyEnabled()
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = new AssetId(customer, space, nameof(Put_NewImageAsset_WithImageOptimisationPolicy_Returns400_IfInvalid_AndLegacyEnabled));
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Spaces.AddTestSpace(customer, space);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(customer);
        await dbContext.DeliveryChannelPolicies.AddTestDeliveryChannelPolicies(customer);
        await dbContext.SaveChangesAsync();

        var hydraImageBody = $@"{{
          ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
          ""imageOptimisationPolicy"" : ""foo""
        }}";
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customer).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
       
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("'foo' is not a valid imageOptimisationPolicy for an image");
    }
    
    [Theory]
    [InlineData("default")]
    [InlineData("https://api.dlc.services/thumbnailPolicies/default")]
    public async Task Put_NewImageAsset_WithThumbnailPolicy_Creates_Asset_WhenLegacyEnabled(string thumbnailPolicy)
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = new AssetId(customer, space, nameof(Put_NewImageAsset_WithThumbnailPolicy_Creates_Asset_WhenLegacyEnabled));

        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Spaces.AddTestSpace(customer, space);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(customer);
        await dbContext.DeliveryChannelPolicies.AddTestDeliveryChannelPolicies(customer);
        await dbContext.SaveChangesAsync();

        var hydraImageBody = $@"{{
          ""@type"": ""Image"",
          ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
          ""thumbnailPolicy"": ""{thumbnailPolicy}""
        }}";

        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);

        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customer).PutAsync(assetId.ToApiResourcePath(), content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(i => i.DeliveryChannelPolicy).Single(i => i.Id == assetId);
        asset.Id.Should().Be(assetId);
        asset.MediaType.Should().Be("image/tiff");
        asset.Family.Should().Be(AssetFamily.Image);
        asset.ThumbnailPolicy.Should().Be("default");
        asset.ImageOptimisationPolicy.Should().BeEmpty();
        asset.ImageDeliveryChannels.Count.Should().Be(2);
        asset.ImageDeliveryChannels.Should().ContainSingle(dc => dc.Channel == AssetDeliveryChannels.Image &&
                                                                dc.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ImageDefault);
        asset.ImageDeliveryChannels.Should().ContainSingle(dc => dc.Channel == AssetDeliveryChannels.Thumbnails &&
                                                                dc.DeliveryChannelPolicy.Name == "default");
    }
    
    [Fact]
    public async Task Put_NewImageAsset_WithThumbnailPolicy_Returns400_IfInvalid_AndLegacyEnabled()
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = new AssetId(customer, space, nameof(Put_NewImageAsset_WithImageOptimisationPolicy_Returns400_IfInvalid_AndLegacyEnabled));
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Spaces.AddTestSpace(customer, space);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(customer);
        await dbContext.DeliveryChannelPolicies.AddTestDeliveryChannelPolicies(customer);
        await dbContext.SaveChangesAsync();

        var hydraImageBody = $@"{{
          ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
          ""thumbnailPolicy"" : ""foo""
        }}";
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customer).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
       
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("'foo' is not a valid thumbnailPolicy for an image");
    }
    
    [Theory]
    [InlineData("video-max")]
    [InlineData("https://api.dlc.services/imageOptimisationPolicy/video-max")]
    public async Task Put_NewVideoAsset_WithImageOptimisationPolicy_Creates_Asset_WhenLegacyEnabled(string imageOptimisationPolicy)
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = new AssetId(customer, space, nameof(Put_NewVideoAsset_WithImageOptimisationPolicy_Creates_Asset_WhenLegacyEnabled));
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Spaces.AddTestSpace(customer, space);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(customer);
        await dbContext.DeliveryChannelPolicies.AddTestDeliveryChannelPolicies(customer);
        await dbContext.SaveChangesAsync();

        var hydraImageBody = $@"{{
          ""family"": ""T"",
          ""origin"": ""https://example.org/{assetId.Asset}.mp4"",
          ""imageOptimisationPolicy"" : ""{imageOptimisationPolicy}""
        }}";

        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(true);

        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customer).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(i => i.DeliveryChannelPolicy).Single(i => i.Id == assetId);
        asset.Id.Should().Be(assetId);
        asset.MediaType.Should().Be("video/mp4");
        asset.Family.Should().Be(AssetFamily.Timebased);
        asset.ImageOptimisationPolicy.Should().Be(imageOptimisationPolicy);
        asset.ThumbnailPolicy.Should().BeEmpty();
        asset.ImageDeliveryChannels.Count.Should().Be(1);
        asset.ImageDeliveryChannels.Should().ContainSingle(dc => dc.Channel == AssetDeliveryChannels.Timebased &&
                                                                dc.DeliveryChannelPolicy.Name == "default-video");
    }
    
    [Fact]
    public async Task Put_NewVideoAsset_WithImageOptimisationPolicy_Returns400_IfInvalid_AndLegacyEnabled()
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = new AssetId(customer, space, nameof(Put_NewVideoAsset_WithImageOptimisationPolicy_Returns400_IfInvalid_AndLegacyEnabled));
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Spaces.AddTestSpace(customer, space);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(customer);
        await dbContext.DeliveryChannelPolicies.AddTestDeliveryChannelPolicies(customer);
        await dbContext.SaveChangesAsync();

        var hydraImageBody = $@"{{
          ""family"": ""T"",
          ""origin"": ""https://example.org/{assetId.Asset}.mp4"",
          ""imageOptimisationPolicy"" : ""foo""
        }}";
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customer).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
       
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("'foo' is not a valid imageOptimisationPolicy for a timebased asset");
    }
    
    [Theory]
    [InlineData("audio-max")]
    [InlineData("https://api.dlc.services/imageOptimisationPolicy/audio-max")]
    public async Task Put_NewAudioAsset_WithImageOptimisationPolicy_Creates_Asset_WhenLegacyEnabled(string imageOptimisationPolicy)
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = new AssetId(customer, space, nameof(Put_NewAudioAsset_WithImageOptimisationPolicy_Creates_Asset_WhenLegacyEnabled));
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Spaces.AddTestSpace(customer, space);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(customer);
        await dbContext.DeliveryChannelPolicies.AddTestDeliveryChannelPolicies(customer);
        await dbContext.SaveChangesAsync();

        var hydraImageBody = $@"{{
          ""family"": ""T"",
          ""origin"": ""https://example.org/{assetId.Asset}.mp3"",
          ""imageOptimisationPolicy"" : ""{imageOptimisationPolicy}""
        }}";

        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(true);

        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customer).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(i => i.DeliveryChannelPolicy).Single(i => i.Id == assetId);
        asset.Id.Should().Be(assetId);
        asset.MediaType.Should().Be("audio/mp3");
        asset.Family.Should().Be(AssetFamily.Timebased);
        asset.ImageOptimisationPolicy.Should().Be(imageOptimisationPolicy);
        asset.ThumbnailPolicy.Should().BeEmpty();
        asset.ImageDeliveryChannels.Count.Should().Be(1);
        asset.ImageDeliveryChannels.Should().ContainSingle(dc => dc.Channel == AssetDeliveryChannels.Timebased &&
                                                                dc.DeliveryChannelPolicy.Name == "default-audio");
    }
    
    [Fact]
    public async Task Put_NewAudioAsset_WithImageOptimisationPolicy_Returns400_IfInvalid_AndLegacyEnabled()
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = new AssetId(customer, space, nameof(Put_NewAudioAsset_WithImageOptimisationPolicy_Returns400_IfInvalid_AndLegacyEnabled));
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Spaces.AddTestSpace(customer, space);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(customer);
        await dbContext.DeliveryChannelPolicies.AddTestDeliveryChannelPolicies(customer);
        await dbContext.SaveChangesAsync();

        var hydraImageBody = $@"{{
          ""family"": ""T"",
          ""origin"": ""https://example.org/{assetId.Asset}.mp3"",
          ""imageOptimisationPolicy"" : ""foo""
        }}";
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customer).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
       
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("'foo' is not a valid imageOptimisationPolicy for a timebased asset");
    }
    
    [Fact]
    public async Task Put_NewFileAsset_Creates_Asset_WhenLegacyEnabled()
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = new AssetId(customer, space, nameof(Put_NewImageAsset_WithThumbnailPolicy_Creates_Asset_WhenLegacyEnabled));

        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Spaces.AddTestSpace(customer, space);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(customer);
        await dbContext.DeliveryChannelPolicies.AddTestDeliveryChannelPolicies(customer);
        await dbContext.SaveChangesAsync();

        var hydraImageBody = $@"{{
          ""@type"": ""Image"",
          ""family"": ""F"",
          ""origin"": ""https://example.org/{assetId.Asset}.pdf"",
        }}";

        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);

        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customer).PutAsync(assetId.ToApiResourcePath(), content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(i => i.DeliveryChannelPolicy).Single(i => i.Id == assetId);
        asset.Id.Should().Be(assetId);
        asset.MediaType.Should().Be("application/pdf");
        asset.Family.Should().Be(AssetFamily.File);
        asset.ThumbnailPolicy.Should().BeEmpty();
        asset.ImageOptimisationPolicy.Should().BeEmpty();
        asset.ImageDeliveryChannels.Count.Should().Be(1);
        asset.ImageDeliveryChannels.Should().ContainSingle(dc => dc.Channel == AssetDeliveryChannels.File &&
                                                                dc.DeliveryChannelPolicy.Name == "none");
    }
}