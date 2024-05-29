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
    private readonly IAmazonS3 amazonS3;
    private static readonly IEngineClient EngineClient = A.Fake<IEngineClient>();

    public ModifyAssetWithOldDeliveryChannelPropertiesTests(
        StorageFixture storageFixture,
        ProtagonistAppFactory<Startup> factory)
    {
        var dbFixture = storageFixture.DbFixture;
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();

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
        var test = response.Content.ReadAsStringAsync();
        
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(i => i.DeliveryChannelPolicy).Single(i => i.Id == assetId);
        asset.Id.Should().Be(assetId);
        asset.MediaType.Should().Be("image/tiff");
        asset.Family.Should().Be(AssetFamily.Image);
        asset.ThumbnailPolicy.Should().Be("default");
        asset.ImageOptimisationPolicy.Should().BeEmpty();
        asset.ImageDeliveryChannels.Count.Should().Be(2);
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "iiif-img" &&
                                                                x.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ImageDefault);
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "thumbs" &&
                                                                x.DeliveryChannelPolicy.Name == "default");
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
          ""@type"": ""Image"",
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
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "iiif-img" &&
                                                                x.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ImageDefault);
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "thumbs" &&
                                                                x.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ThumbsDefault);
    }
}