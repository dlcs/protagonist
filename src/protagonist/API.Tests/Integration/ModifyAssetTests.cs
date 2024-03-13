using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using API.Client;
using API.Infrastructure.Messaging;
using API.Tests.Integration.Infrastructure;
using DLCS.Core.Types;
using DLCS.HydraModel;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Repository;
using DLCS.Repository.Entities;
using DLCS.Repository.Messaging;
using FakeItEasy;
using Hydra.Collections;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;
using AssetFamily = DLCS.Model.Assets.AssetFamily;
using ImageOptimisationPolicy = DLCS.Model.Policies.ImageOptimisationPolicy;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(StorageCollection.CollectionName)]
public class ModifyAssetTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsContext dbContext;
    private readonly HttpClient httpClient;
    private static readonly IAssetNotificationSender NotificationSender = A.Fake<IAssetNotificationSender>();
    private readonly IAmazonS3 amazonS3;
    private static readonly IEngineClient EngineClient = A.Fake<IEngineClient>();
    
    public ModifyAssetTests(
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
            .WithConfigValue("DeliveryChannelsEnabled", "true")
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        
        dbFixture.CleanUp();
    }

    [Fact]
    public async Task Put_NewImageAsset_Creates_Asset()
    {
        var customerAndSpace = await CreateCustomerAndSpace();

        var assetId = new AssetId(customerAndSpace.customer, customerAndSpace.space, nameof(Put_NewImageAsset_Creates_Asset));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
  ""family"": ""I"",
  ""mediaType"": ""image/tiff""
}}";
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), A<Asset>._, false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerAndSpace.customer).PutAsync(assetId.ToApiResourcePath(), content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.PathAndQuery.Should().Be(assetId.ToApiResourcePath());
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels).Single(x => x.Id == assetId);
        asset.Id.Should().Be(assetId);
        asset.MaxUnauthorised.Should().Be(-1);
        asset.ImageDeliveryChannels.Count.Should().Be(2);
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "iiif-img");
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "thumbs");
    }
    
    [Fact]
    public async Task Put_NewImageAsset_Creates_Asset_WithCustomDefaultDeliveryChannel()
    {
        var customerAndSpace = await CreateCustomerAndSpace();

        var newPolicy = await dbContext.DeliveryChannelPolicies.AddAsync(new DLCS.Model.Policies.DeliveryChannelPolicy()
        {
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            DisplayName = "test policy - space specific",
            PolicyData = null,
            Name = "space-specific-image",
            Channel = "iiif-img",
            Customer = customerAndSpace.customer,
            Id = 260
        });

        await dbContext.DefaultDeliveryChannels.AddAsync(new DLCS.Model.DeliveryChannels.DefaultDeliveryChannel
        {
            Space = customerAndSpace.space,
            Customer = customerAndSpace.customer,
            DeliveryChannelPolicyId = newPolicy.Entity.Id,
            MediaType = "image/tiff"
        });

        await dbContext.SaveChangesAsync();

        var assetId = new AssetId(customerAndSpace.customer, customerAndSpace.space, nameof(Put_NewImageAsset_Creates_Asset));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
  ""family"": ""I"",
  ""mediaType"": ""image/tiff""
}}";
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), A<Asset>._, false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerAndSpace.customer).PutAsync(assetId.ToApiResourcePath(), content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.PathAndQuery.Should().Be(assetId.ToApiResourcePath());
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels).Single(x => x.Id == assetId);
        asset.Id.Should().Be(assetId);
        asset.MaxUnauthorised.Should().Be(-1);
        asset.ImageDeliveryChannels.Count.Should().Be(2);
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "iiif-img" &&
                                                                x.DeliveryChannelPolicyId == newPolicy.Entity.Id);
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "thumbs");
    }
    
    [Fact]
    public async Task Put_NewImageAsset_Creates_AssetWithSpecifiedDeliveryChannels()
    {
        var customerAndSpace = await CreateCustomerAndSpace();

        var assetId = new AssetId(customerAndSpace.customer, customerAndSpace.space, nameof(Put_NewImageAsset_Creates_Asset));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
  ""family"": ""I"",
  ""mediaType"": ""image/tiff"",
   ""deliveryChannels"": [
{{
    ""channel"":""iiif-img"",
    ""policy"":""default""
 }}, 
{{
    ""channel"":""thumbs"",
    ""policy"":""https://some.url/customers/{customerAndSpace.customer}/deliveryChannelPolicies/thumbs/default""
 }}
] 
}}";
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), A<Asset>._, false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerAndSpace.customer).PutAsync(assetId.ToApiResourcePath(), content);

        var stuff = await response.Content.ReadAsStringAsync();

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.PathAndQuery.Should().Be(assetId.ToApiResourcePath());
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels).Single(x => x.Id == assetId);
        asset.Id.Should().Be(assetId);
        asset.MaxUnauthorised.Should().Be(-1);
        asset.ImageDeliveryChannels.Count.Should().Be(2);
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "iiif-img");
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "thumbs");
    }
    
     [Fact]
    public async Task Put_NewImageAsset_Creates_Asset_WhileIgnoringCustomDefaultDeliveryChannel()
    {
        var customerAndSpace = await CreateCustomerAndSpace();

        var newPolicy = await dbContext.DeliveryChannelPolicies.AddAsync(new DLCS.Model.Policies.DeliveryChannelPolicy()
        {
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            DisplayName = "test policy - space specific",
            PolicyData = null,
            Name = "space-specific-image",
            Channel = "iiif-img",
            Customer = customerAndSpace.customer,
            Id = 260
        });

        await dbContext.DefaultDeliveryChannels.AddAsync(new DLCS.Model.DeliveryChannels.DefaultDeliveryChannel
        {
            Space = customerAndSpace.space + 1,
            Customer = customerAndSpace.customer,
            DeliveryChannelPolicyId = newPolicy.Entity.Id,
            MediaType = "image/tiff"
        });

        await dbContext.SaveChangesAsync();

        var assetId = new AssetId(customerAndSpace.customer, customerAndSpace.space, nameof(Put_NewImageAsset_Creates_Asset));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
  ""family"": ""I"",
  ""mediaType"": ""image/tiff""
}}";
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), A<Asset>._, false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerAndSpace.customer).PutAsync(assetId.ToApiResourcePath(), content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.PathAndQuery.Should().Be(assetId.ToApiResourcePath());
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels).Single(x => x.Id == assetId);
        asset.Id.Should().Be(assetId);
        asset.MaxUnauthorised.Should().Be(-1);
        asset.ImageDeliveryChannels.Count.Should().Be(2);
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "iiif-img" &&
                                                                x.DeliveryChannelPolicyId == 1);
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "thumbs");
    }
    
    [Theory]
    [InlineData("T", "video/mp4", "mp4",  "iiif-img")]
    [InlineData("T", "video/mp4", "mp4", "thumbs")]
    [InlineData("I", "image/jpeg", "jpeg", "iiif-av")]
    [InlineData("F", "application/pdf", "pdf", "iiif-img")]
    [InlineData("F", "application/pdf", "pdf", "thumbs")]
    [InlineData("F", "application/pdf", "pdf", "iiif-av")]
    public async Task Put_NewImageAsset_BadRequest_WhenDeliveryChannelInvalidForMediaType(string family, string mediaType, string format, string deliveryChannel)
    {
        // arrange
        var assetId = new AssetId(99, 1, $"{nameof(Put_NewImageAsset_BadRequest_WhenDeliveryChannelInvalidForMediaType)}-{deliveryChannel}-{format}");
        var hydraImageBody = $@"{{
            ""@type"": ""Image"",
            ""origin"": ""https://example.org/{assetId.Asset}.{format}"",
            ""family"": ""{family}"",
            ""mediaType"": ""{mediaType}"",
            ""deliveryChannels"": [
            {{
                ""channel"":""{deliveryChannel}"",
                ""policy"":""default""
            }}]
        }}";
     
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Put_NewImageAsset_BadRequest_WhenDeliveryChannels_ContainsDuplicates()
    {
        var assetId = new AssetId(99, 1, nameof(Put_NewImageAsset_BadRequest_WhenDeliveryChannels_ContainsDuplicates));
        var hydraImageBody = $@"{{
            ""@type"": ""Image"",
            ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
            ""family"": ""I"",
            ""mediaType"": ""image/tiff"",
            ""deliveryChannels"": [
            {{
                ""channel"":""iiif-img"",
                ""policy"":""default""
            }},
            {{
                ""channel"":""iiif-img"",
                ""policy"":""default""
            }},
            {{
                ""channel"":""file"",
                ""policy"":""none""
            }}]]
        }}";
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    private async Task<(int customer, int space)> CreateCustomerAndSpace()
    {
        const string newCustomerJson = @"{
  ""@type"": ""Customer"",
  ""name"": ""api-test-asset-1"",
  ""displayName"": ""My New Customer for asset""
}";
        var customerContent = new StringContent(newCustomerJson, Encoding.UTF8, "application/json");
        var customerResponse = await httpClient.AsAdmin().PostAsync("/customers", customerContent);
        var customer = await customerResponse.ReadAsHydraResponseAsync<Customer>();
        var customerId = int.Parse(customer.Id!.Split('/').Last());
        
        const string newSpaceJson = @"{
  ""@type"": ""Space"",
  ""name"": ""Test Space""
}";
        var spaceContent = new StringContent(newSpaceJson, Encoding.UTF8, "application/json");
        var spacePostUrl = $"/customers/{customerId}/spaces";
        var spaceResponse = await httpClient.AsCustomer(customerId).PostAsync(spacePostUrl, spaceContent);
        var space = await spaceResponse.ReadAsHydraResponseAsync<Space>();
        var spaceId = int.Parse(space.Id!.Split('/').Last());

        return (customerId, spaceId);
    }

    [Fact]
    public async Task Put_NewImageAsset_BadRequest_WhenCalledWithInvalidId()
    {
        var assetId = new AssetId(99, 1, "invalid id");
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
  ""family"": ""I"",
  ""mediaType"": ""image/tiff""
}}";
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), A<Asset>._, false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Put_NewImageAsset_FailsToCreateAsset_whenMediaTypeAndFamilyNotSet()
    {
        var assetId = new AssetId(99, 1, nameof(Put_NewImageAsset_FailsToCreateAsset_whenMediaTypeAndFamilyNotSet));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff""
}}";
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), A<Asset>._, false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Put_NewImageAsset_CreatesAsset_whenMediaTypeAndFamilyNotSetWithLegacyEnabled()
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = new AssetId(customer, space, nameof(Put_NewImageAsset_CreatesAsset_whenMediaTypeAndFamilyNotSetWithLegacyEnabled));
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Spaces.AddTestSpace(customer, space);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(customer);
        await dbContext.SaveChangesAsync();
        
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff""
}}";
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), A<Asset>._, false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customer).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels).Single(i => i.Id == assetId);
        asset.Id.Should().Be(assetId);
        asset.MediaType.Should().Be("image/tiff");
        asset.Family.Should().Be(AssetFamily.Image);
        asset.ImageDeliveryChannels.Count.Should().Be(2);
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "iiif-img" &&
                                                                x.DeliveryChannelPolicyId == 1);
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "thumbs");
    }
    
    [Fact]
    public async Task Put_NewImageAsset_CreatesAsset_whenInferringOfMediaTypeNotPossibleWithLegacyEnabled()
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = new AssetId(customer, space, nameof(Put_NewImageAsset_CreatesAsset_whenMediaTypeAndFamilyNotSetWithLegacyEnabled));
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Spaces.AddTestSpace(customer, space);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(customer);
        await dbContext.SaveChangesAsync();
        
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}""
}}";
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), A<Asset>._, false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customer).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels).Single(i => i.Id == assetId);
        asset.Id.Should().Be(assetId);
        asset.MediaType.Should().Be("image/unknown");
        asset.Family.Should().Be(AssetFamily.Image);
        asset.ImageDeliveryChannels.Count.Should().Be(2);
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "iiif-img" &&
                                                                x.DeliveryChannelPolicyId == 1);
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "thumbs");
    }
    
    [Theory]
    [InlineData("audio/mp3", AssetFamily.Timebased)]
    [InlineData("video/mp4", AssetFamily.Timebased)]
    public async Task Put_NewImageAsset_SetsFamilyBasedOnMediaType_IfFamilyAndDeliveryChannelsMissing_Async(string mediaType, AssetFamily expectedFamily)
    {
        var urlSafeMediaType = mediaType.Replace("/", "_");
        var assetId = new AssetId(99, 1,
            $"{nameof(Put_NewImageAsset_SetsFamilyBasedOnMediaType_IfFamilyAndDeliveryChannelsMissing_Async)}_{urlSafeMediaType}");
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
  ""mediaType"": ""{mediaType}""
}}";
        
        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId),
                    A<Asset>._,
                    A<CancellationToken>._))
            .Returns(true);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.PathAndQuery.Should().Be(assetId.ToApiResourcePath());
        var asset = await dbContext.Images.FindAsync(assetId);
        asset.Id.Should().Be(assetId);
        asset.Family.Should().Be(expectedFamily);
    }
    
    [Theory]
    [InlineData("image/jpeg", AssetFamily.Image)]
    [InlineData("application/pdf", AssetFamily.File)]
    public async Task Put_NewImageAsset_SetsFamilyBasedOnMediaType_IfFamilyAndDeliveryChannelsMissing_Sync(string mediaType, AssetFamily expectedFamily)
    {
        var urlSafeMediaType = mediaType.Replace("/", "_");
        var assetId = new AssetId(99, 1,
            $"{nameof(Put_NewImageAsset_SetsFamilyBasedOnMediaType_IfFamilyAndDeliveryChannelsMissing_Sync)}_{urlSafeMediaType}");
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
  ""mediaType"": ""{mediaType}""
}}";
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId),  
                    A<Asset>._, 
                    false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);

        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.PathAndQuery.Should().Be(assetId.ToApiResourcePath());
        var asset = await dbContext.Images.FindAsync(assetId);
        asset.Id.Should().Be(assetId);
        asset.Family.Should().Be(expectedFamily);
    }

    [Fact]
    public async Task Put_NewImageAsset_Creates_Asset_SetsCounters()
    {
        const int customer = 99239;
        const int space = 991;
        var assetId = new AssetId(customer, space, nameof(Put_NewImageAsset_Creates_Asset_SetsCounters));
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Spaces.AddTestSpace(customer, space);
        await dbContext.SaveChangesAsync();

        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
  ""family"": ""I"",
  ""mediaType"": ""image/tiff""
}}";
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), 
                    A<Asset>._,
                    false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.PathAndQuery.Should().Be(assetId.ToApiResourcePath());
        
        var customerCounter = await dbContext.EntityCounters.SingleAsync(ec =>
            ec.Customer == 0 && ec.Type == "customer-images" && ec.Scope == customer.ToString());
        customerCounter.Next.Should().Be(1);
        var spaceCounter = await dbContext.EntityCounters.SingleAsync(ec =>
            ec.Customer == customer && ec.Type == "space-images" && ec.Scope == space.ToString());
        spaceCounter.Next.Should().Be(1);
    }

    [Fact]
    public async Task Put_NewImageAsset_ReturnsEngineStatusCode_IfEngineRequestFails()
    {
        var assetId = new AssetId(99, 1, nameof(Put_NewImageAsset_ReturnsEngineStatusCode_IfEngineRequestFails));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
  ""family"": ""I"",
  ""mediaType"": ""image/tiff""
}}";
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), 
                    A<Asset>._,
                    false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.TooManyRequests);  // Random status to verify it filters down
        
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.Location.Should().BeNull();
        var asset = await dbContext.Images.FindAsync(assetId);
        asset.Id.Should().Be(assetId);
    }
    
    [Fact]
    public async Task Put_NewImageAsset_Creates_Asset_WithImageBytesProvided()
    {
        // Arrange
        var assetId = new AssetId(99, 1, nameof(Put_NewImageAsset_Creates_Asset_WithImageBytesProvided));
        var hydraBody = await File.ReadAllTextAsync("Direct_Bytes_Upload.json");
        var hydraJson = JsonConvert.DeserializeObject<ImageWithFile>(hydraBody);
        
        A.CallTo(() =>
            EngineClient.SynchronousIngest(
                A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), 
                A<Asset>._,
                false,
                A<CancellationToken>._))
        .Returns(HttpStatusCode.OK);
        
        // Act
        var content = new StringContent(hydraBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PostAsync(assetId.ToApiResourcePath(), content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.PathAndQuery.Should().Be(assetId.ToApiResourcePath());
        
        // The asset was created and has the correct media type
        var asset = await dbContext.Images.FindAsync(assetId);
        asset.Id.Should().Be(assetId);
        asset.MediaType.Should().Be(hydraJson.MediaType);
        
        // The image was saved to S3 with correct header
        var item = await amazonS3.GetObjectAsync(LocalStackFixture.OriginBucketName, assetId.ToString());
        item.Headers.ContentType.Should().Be(hydraJson.MediaType, "Media type set on stored asset");
        var storedBytes = StreamToBytes(item.ResponseStream);
        storedBytes.Should().BeEquivalentTo(hydraJson.File, "Correct file bytes stored");
    }
    
    [Fact(Skip = "Is this an expected behaviour?")]
    public async Task Put_SetsError_IfEngineRequestFails()
    {
        var assetId = new AssetId(99, 1, nameof(Put_SetsError_IfEngineRequestFails));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
}}";

        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId),
                    A<Asset>._,
        false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.InternalServerError);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var assetFromDatabase = await dbContext.Images.SingleOrDefaultAsync(a => a.Id == assetId);
        assetFromDatabase.Ingesting.Should().BeFalse();
        assetFromDatabase.Error.Should().NotBeEmpty();
    }
    
    [Fact]
    public async Task Put_NewAudioAsset_Creates_Asset()
    {
        var assetId = new AssetId(99, 1, nameof(Put_NewAudioAsset_Creates_Asset));
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(99);
        await dbContext.SaveChangesAsync();
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.mp4"",
  ""family"": ""T"",
  ""mediaType"": ""audio/mp4""
}}";
        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId),
                    A<Asset>._,
                    A<CancellationToken>._))
            .Returns(true);
        
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");

        // act
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.PathAndQuery.Should().Be(assetId.ToApiResourcePath());
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels).Single(x => x.Id == assetId);
        asset.Id.Should().Be(assetId);
        asset.MaxUnauthorised.Should().Be(-1);
        asset.ImageDeliveryChannels.Count.Should().Be(1);
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "iiif-av" &&
                                                                x.DeliveryChannelPolicyId == 5);
    }
    
    [Fact]
    public async Task Put_NewVideoAsset_Creates_Asset()
    {
        var assetId = new AssetId(99, 1, nameof(Put_NewVideoAsset_Creates_Asset));
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(99);
        await dbContext.SaveChangesAsync();
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.mp4"",
  ""family"": ""T"",
  ""mediaType"": ""video/mp4""
}}";
        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), 
                    A<Asset>._,
                    A<CancellationToken>._))
            .Returns(true);
        
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");

        // act
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.PathAndQuery.Should().Be(assetId.ToApiResourcePath());
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels).Single(x => x.Id == assetId);
        asset.Id.Should().Be(assetId);
        asset.MaxUnauthorised.Should().Be(-1);
        asset.ImageDeliveryChannels.Count.Should().Be(1);
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "iiif-av" &&
                                                                x.DeliveryChannelPolicyId == 6);
    }
    
    [Fact]
    public async Task Put_NewFileAsset_Creates_Asset()
    {
        var assetId = new AssetId(99, 1, nameof(Put_NewFileAsset_Creates_Asset));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.pdf"",
  ""family"": ""F"",
  ""mediaType"": ""application/pdf""
}}";
        
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), 
                    A<Asset>._,
                    false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);

        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");

        // act
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        response.Headers.Location.PathAndQuery.Should().Be(assetId.ToApiResourcePath());
        var asset = await dbContext.Images.FindAsync(assetId);
        asset.Id.Should().Be(assetId);
        asset.MaxUnauthorised.Should().Be(-1);
        asset.ThumbnailPolicy.Should().BeEmpty();
        asset.ImageOptimisationPolicy.Should().BeEmpty();
    }
    
    [Fact]
    public async Task Put_NewTimebasedAsset_Returns500_IfEnqueueFails()
    {
        var assetId = new AssetId(99, 1, nameof(Put_NewTimebasedAsset_Returns500_IfEnqueueFails));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.mp4"",
  ""family"": ""T"",
  ""mediaType"": ""video/mp4""
}}";
        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), 
                    A<Asset>._,
                    A<CancellationToken>._))
            .Returns(false);
        
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Headers.Location.Should().BeNull();
        var asset = await dbContext.Images.FindAsync(assetId);
        asset.Id.Should().Be(assetId);
    }

    [Fact]
    public async Task Put_New_Asset_Requires_Origin()
    {
        var assetId = new AssetId(99, 1, nameof(Put_New_Asset_Requires_Origin));
        var hydraImageBody = @"{{
  ""@type"": ""Image""
}}";
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Put_New_Asset_Supports_WcDeliveryChannels()
    {
        var assetId = new AssetId(99, 1, nameof(Put_New_Asset_Supports_WcDeliveryChannels));
        var hydraImageBody = $@"{{
          ""@type"": ""Image"",
          ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
          ""family"": ""I"",
          ""mediaType"": ""image/tiff"",
          ""wcDeliveryChannels"": [""file""]
        }}";

        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), 
                    A<Asset>._,
                    false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var asset = await dbContext.Images.FindAsync(assetId);
        asset.DeliveryChannels.Should().BeEquivalentTo("file");
    }

    [Fact]
    public async Task Put_Existing_Asset_ClearsError_AndMarksAsIngesting()
    {
        var assetId = new AssetId(99, 1, nameof(Put_Existing_Asset_ClearsError_AndMarksAsIngesting));
        var newAsset = await dbContext.Images.AddTestAsset(assetId, error: "Sample Error");
        await dbContext.SaveChangesAsync();
        
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
  ""family"": ""I"",
  ""mediaType"": ""image/tiff""
}}";
        
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), 
                    A<Asset>._,
                    false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        await dbContext.Entry(newAsset.Entity).ReloadAsync();
        newAsset.Entity.Ingesting.Should().BeTrue();
        newAsset.Entity.Error.Should().BeEmpty();
    }
    
    [Fact]
    public async Task Put_Asset_Returns_InsufficientStorage_if_Policy_Exceeded()
    {
        // This will break other tests so we need a different customer
        // This customer has maxed out their limit of 2!
        const int customer = 599;
        await dbContext.Customers.AddAsync(new DLCS.Model.Customers.Customer
        {
            Created = DateTime.UtcNow,
            Id = customer,
            DisplayName = "TinyUser",
            Name = "tinycustomer",
            Keys = Array.Empty<string>()
        });
        await dbContext.Spaces.AddTestSpace(customer, 1, "tiny-cust-space");
        await dbContext.StoragePolicies.AddAsync(new DLCS.Model.Storage.StoragePolicy()
        {
            Id = "tiny",
            MaximumNumberOfStoredImages = 2,
            MaximumTotalSizeOfStoredImages = 1000000
        });
        await dbContext.CustomerStorages.AddAsync(new DLCS.Model.Storage.CustomerStorage
        {
            StoragePolicy = "tiny",
            Customer = customer, Space = 0,
            TotalSizeOfStoredImages = 0,
            TotalSizeOfThumbnails = 0,
            NumberOfStoredImages = 2
        });
        await dbContext.SaveChangesAsync();

        var assetId = new AssetId(customer, 1, nameof(Put_Asset_Returns_InsufficientStorage_if_Policy_Exceeded));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
  ""family"": ""I"",
  ""mediaType"": ""image/tiff""
}}";
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customer).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.InsufficientStorage);
    }

    [Theory]
    [InlineData(AssetFamily.Image)]
    [InlineData(AssetFamily.Timebased)] 
    public async Task Patch_Asset_Updates_Asset_Without_Calling_Engine(AssetFamily family)
    {
        var assetId = new AssetId(99, 1, $"{nameof(Patch_Asset_Updates_Asset_Without_Calling_Engine)}{family}");

        var testAsset = await dbContext.Images.AddTestAsset(assetId, family: family,
            ref1: "I am string 1", origin: "https://images.org/image2.tiff");
        await dbContext.SaveChangesAsync();

        var hydraImageBody = @"{
  ""@type"": ""Image"",
  ""string1"": ""I am edited""
}";
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), 
                    A<Asset>._,
                    false,
                    A<CancellationToken>._))
            .MustNotHaveHappened();
        
        await dbContext.Entry(testAsset.Entity).ReloadAsync();
        testAsset.Entity.Reference1.Should().Be("I am edited");
    }
    
    [Fact]
    public async Task Patch_ImageAsset_Updates_Asset_And_Calls_Engine_If_Reingest_Required()
    {
        var assetId = new AssetId(99, 1, nameof(Patch_ImageAsset_Updates_Asset_And_Calls_Engine_If_Reingest_Required));

        var testAsset = await dbContext.Images.AddTestAsset(assetId,
            ref1: "I am string 1", origin: $"https://example.org/{assetId.Asset}.tiff");
        
        await dbContext.SaveChangesAsync();
        
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}-changed.tiff"",
  ""string1"": ""I am edited""
}}";
        
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId),
                    A<Asset>._,
                    false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);

        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId),  
                    A<Asset>._,
                    false,
                    A<CancellationToken>._))
            .MustHaveHappened();
        
        await dbContext.Entry(testAsset.Entity).ReloadAsync();
        testAsset.Entity.Reference1.Should().Be("I am edited");
    }
    
    [Fact]
    public async Task Patch_TimebasedAsset_Updates_Asset_AndEnqueuesMessage_IfReingestRequired()
    {
        var assetId = new AssetId(99, 1,
            nameof(Patch_TimebasedAsset_Updates_Asset_AndEnqueuesMessage_IfReingestRequired));

        var testAsset = await dbContext.Images.AddTestAsset(assetId, family: AssetFamily.Timebased,
            ref1: "I am string 1", origin: $"https://example.org/{assetId.Asset}.mp4", mediaType: "video/mp4");
        
        await dbContext.SaveChangesAsync();
        
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}-changed.mp4"",
  ""string1"": ""I am edited""
}}";

        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), 
                    A<Asset>._,
                    A<CancellationToken>._))
            .Returns(true);

        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), 
                    A<Asset>._,
                    A<CancellationToken>._))
            .MustHaveHappened();
        
        await dbContext.Entry(testAsset.Entity).ReloadAsync();
        testAsset.Entity.Reference1.Should().Be("I am edited");
        testAsset.Entity.Batch.Should().BeGreaterThan(0);
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
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId),                     
                    A<Asset>._,
                    false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);

        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), 
                    A<Asset>._, 
                    false,
                    A<CancellationToken>._))
            .MustHaveHappened();
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        await dbContext.Entry(asset).ReloadAsync();
        asset.ImageOptimisationPolicy.Should().Be("test-policy");
    }

    [Fact]
    public async Task Patch_Asset_Returns_Notfound_if_Asset_Missing()
    {
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""string1"": ""I am edited""
}}";
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync("99/1/this-image-is-not-there", content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Patch_Images_Updates_Multiple_Images()
    {
        // note "member" not "members"
        await dbContext.Spaces.AddTestSpace(99, 3003, nameof(Patch_Images_Updates_Multiple_Images));
        await dbContext.Images.AddTestAsset(AssetId.FromString("99/3003/asset-0010"), customer: 99, space: 3003,
            ref1: "Asset 0010", ref2: "String2 0010");
        await dbContext.Images.AddTestAsset(AssetId.FromString("99/3003/asset-0011"), customer: 99, space: 3003,
            ref1: "Asset 0011", ref2: "String2 0011");
        await dbContext.Images.AddTestAsset(AssetId.FromString("99/3003/asset-0012"), customer: 99, space: 3003,
            ref1: "Asset 0012", ref2: "String2 0012");

        await dbContext.SaveChangesAsync();
        
        var hydraCollectionBody = @"{
  ""@type"": ""Collection"",
  ""member"": [
   {
        ""@type"": ""Image"",
        ""id"": ""asset-0010"",
        ""string1"": ""Asset 10 patched""
   },
    {
        ""@type"": ""Image"",
        ""id"": ""asset-0011"",
        ""string1"": ""Asset 11 patched""
    },
    {
        ""@type"": ""Image"",
        ""id"": ""asset-0012"",
        ""string1"": ""Asset 12 patched"",
        ""string3"": ""Asset 12 string3 added""
    }   
  ]
}";
        
        // act
        var content = new StringContent(hydraCollectionBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync("/customers/99/spaces/3003/images", content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var coll = await response.ReadAsHydraResponseAsync<HydraCollection<JObject>>();
        coll.Members.Should().HaveCount(3);
        var hydra10 = coll.Members.Single(m =>
            m["@id"].Value<string>().EndsWith("/customers/99/spaces/3003/images/asset-0010"));
        hydra10["string1"].Value<string>().Should().Be("Asset 10 patched");
        var hydra12 = coll.Members.Single(m =>
            m["@id"].Value<string>().EndsWith("/customers/99/spaces/3003/images/asset-0012"));
        hydra12["string1"].Value<string>().Should().Be("Asset 12 patched");
        hydra12["string3"].Value<string>().Should().Be("Asset 12 string3 added");
        
        dbContext.ChangeTracker.Clear();
        var img10 = await dbContext.Images.FindAsync(AssetId.FromString("99/3003/asset-0010"));
        img10.Reference1.Should().Be("Asset 10 patched");
        var img12 = await dbContext.Images.FindAsync(AssetId.FromString("99/3003/asset-0012"));
        img12.Reference1.Should().Be("Asset 12 patched");
        img12.Reference3.Should().Be("Asset 12 string3 added");
    }
    
    [Theory]
    [InlineData("origin","https://example.com/images/example-image.jpg")]
    [InlineData("imageOptimisationPolicy","example-policy")]
    [InlineData("maxUnauthorised","200")]
    [InlineData("deliveryChannel","[\"iiif-img\",\"thumbs\"]")]
    public async Task Patch_Images_Returns400_IfReingestRequired(string field, string value)
    {
        // arrange
        var hydraCollectionBody = $@"{{
          ""@type"": ""Collection"",
          ""member"": [
           {{
                ""@type"": ""Image"",
                ""id"": ""asset-0000"",
                ""{field}"": ""{value}""
           }}]
        }}";
        
        // act
        var content = new StringContent(hydraCollectionBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync("/customers/99/spaces/1/images", content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Patch_Images_Returns400_IfMemberIdMissing()
    {
        // arrange
        var hydraCollectionBody = $@"{{
          ""@type"": ""Collection"",
          ""member"": [
           {{
                ""@type"": ""Image"",
                ""string1"": ""Example""
           }}]
        }}";
        
        // act
        var content = new StringContent(hydraCollectionBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync("/customers/99/spaces/1/images", content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Bulk_Patch_Prevents_Engine_Call()
    {
        var assetId = new AssetId(99, 1, nameof(Bulk_Patch_Prevents_Engine_Call));
        
        await dbContext.Images.AddTestAsset(assetId,
            ref1: "I am string 1", origin:$"https://images.org/{assetId.Asset}.tiff");
        await dbContext.SaveChangesAsync();
        
        // There's only one member here, but we still don't allow engine-calling changes
        // via collections.
        var hydraCollectionBody = $@"{{
  ""@type"": ""Collection"",
  ""member"": [
   {{
        ""@type"": ""Image"",
        ""id"": ""{assetId.Asset}"",
        ""origin"": ""https://images.org/{assetId.Asset}-PATCHED.tiff"",
        ""string1"": ""PATCHED""
   }}
  ]
}}";
        // act
        var content = new StringContent(hydraCollectionBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync("/customers/99/spaces/1/images", content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Post_ImageBytes_Ingests_New_Image()
    {
        var assetId = new AssetId(99, 1, nameof(Post_ImageBytes_Ingests_New_Image));
        var hydraBody = await File.ReadAllTextAsync("Direct_Bytes_Upload.json");
        
        // The test just uses the string form, but we want this to validate later calls more easily
        var hydraJson = JsonConvert.DeserializeObject<ImageWithFile>(hydraBody);

        // make a callback for engine
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), 
                    A<Asset>._,
                    false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PostAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        // The image was saved to S3 with correct header
        var item = await amazonS3.GetObjectAsync(LocalStackFixture.OriginBucketName, assetId.ToString());
        item.Headers.ContentType.Should().Be(hydraJson.MediaType, "Media type set on stored asset");
        var storedBytes = StreamToBytes(item.ResponseStream);
        storedBytes.Should().BeEquivalentTo(hydraJson.File, "Correct file bytes stored");

        // Engine was called during this process.
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId),  A<Asset>._, false,
                    A<CancellationToken>._))
            .MustHaveHappened();
        
        // The API created an Image whose origin is the S3 location
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.PathAndQuery.Should().Be(assetId.ToApiResourcePath());
        var asset = await dbContext.Images.FindAsync(assetId);
        asset.Should().NotBeNull();
        asset.Origin.Should()
            .Be("https://protagonist-origin.s3.eu-west-1.amazonaws.com/99/1/Post_ImageBytes_Ingests_New_Image");
    }

    [Fact]
    public async Task Delete_Returns404_IfAssetNotFound()
    {
        // Arrange
        var assetId = new AssetId(99, 1, nameof(Delete_Returns404_IfAssetNotFound));
        
        // Act
        var response = await httpClient.AsCustomer(99).DeleteAsync(assetId.ToApiResourcePath());
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
        // TODO - test for notification not raised once implemented
    }
    
    [Fact]
    public async Task Delete_RemovesAssetAndAssociatedEntities_FromDb()
    {
        // Arrange
        var assetId = new AssetId(99, 1, nameof(Delete_RemovesAssetAndAssociatedEntities_FromDb));
        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.ImageLocations.AddTestImageLocation(assetId);
        await dbContext.ImageStorages.AddTestImageStorage(assetId, size: 400L, thumbSize: 100L);
        var customerSpaceStorage = await dbContext.CustomerStorages.AddTestCustomerStorage(space: 1, numberOfImages: 100,
            sizeOfStored: 1000L, sizeOfThumbs: 1000L);
        var customerStorage = await dbContext.CustomerStorages.AddTestCustomerStorage(space: 0, numberOfImages: 200,
            sizeOfStored: 2000L, sizeOfThumbs: 2000L);
        var customerImagesCounter = await dbContext.EntityCounters.SingleAsync(ec =>
            ec.Customer == 0 && ec.Scope == "99" && ec.Type == KnownEntityCounters.CustomerImages);
        var currentCustomerImageCount = customerImagesCounter.Next;
        var spaceImagesCounter = await dbContext.EntityCounters.SingleAsync(ec =>
            ec.Customer == 99 && ec.Scope == "1" && ec.Type == KnownEntityCounters.SpaceImages);
        var currentSpaceImagesCounter = spaceImagesCounter.Next;
        await dbContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.AsCustomer(99).DeleteAsync(assetId.ToApiResourcePath());
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        // Asset, Location + Storage deleted
        var dbAsset = await dbContext.Images.SingleOrDefaultAsync(i => i.Id == assetId);
        dbAsset.Should().BeNull();
        var dbLocation = await dbContext.ImageLocations.SingleOrDefaultAsync(i => i.Id == assetId);
        dbLocation.Should().BeNull();
        
        var dbStorage = await dbContext.ImageStorages.SingleOrDefaultAsync(i => i.Id == assetId);
        dbStorage.Should().BeNull();
        
        // CustomerStorage values reduced
        await dbContext.Entry(customerSpaceStorage.Entity).ReloadAsync();
        customerSpaceStorage.Entity.NumberOfStoredImages.Should().Be(99);
        customerSpaceStorage.Entity.TotalSizeOfThumbnails.Should().Be(900L);
        customerSpaceStorage.Entity.TotalSizeOfStoredImages.Should().Be(600L);
        
        await dbContext.Entry(customerStorage.Entity).ReloadAsync();
        customerStorage.Entity.NumberOfStoredImages.Should().Be(199);
        customerStorage.Entity.TotalSizeOfThumbnails.Should().Be(1900L);
        customerStorage.Entity.TotalSizeOfStoredImages.Should().Be(1600L);
        
        // EntityCounter for customer images reduced
        var dbCustomerCounter = await dbContext.EntityCounters.SingleAsync(ec =>
            ec.Customer == 0 && ec.Scope == "99" && ec.Type == KnownEntityCounters.CustomerImages);
        dbCustomerCounter.Next.Should().Be(currentCustomerImageCount - 1);
        
        // EntityCounter for space images reduced
        var dbSpaceCounter = await dbContext.EntityCounters.SingleAsync(ec =>
            ec.Customer == 99 && ec.Scope == "1" && ec.Type == KnownEntityCounters.SpaceImages);
        dbSpaceCounter.Next.Should().Be(currentSpaceImagesCounter - 1);

        A.CallTo(() => NotificationSender.SendAssetModifiedMessage(
            A<AssetModificationRecord>.That.Matches(r => r.ChangeType == ChangeType.Delete && r.DeleteFrom == ImageCacheType.None), 
            A<CancellationToken>._)).MustHaveHappened();
    }
    
        [Fact]
    public async Task Delete_NotifiesForCdnAndInternalCacheRemoval_FromAssetNotified()
    {
        // Arrange
        var assetId = new AssetId(99, 1, nameof(Delete_RemovesAssetAndAssociatedEntities_FromDb));
        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.ImageLocations.AddTestImageLocation(assetId);
        await dbContext.ImageStorages.AddTestImageStorage(assetId, size: 400L, thumbSize: 100L);
        var customerSpaceStorage = await dbContext.CustomerStorages.AddTestCustomerStorage(space: 1, numberOfImages: 100,
            sizeOfStored: 1000L, sizeOfThumbs: 1000L);
        var customerStorage = await dbContext.CustomerStorages.AddTestCustomerStorage(space: 0, numberOfImages: 200,
            sizeOfStored: 2000L, sizeOfThumbs: 2000L);
        var customerImagesCounter = await dbContext.EntityCounters.SingleAsync(ec =>
            ec.Customer == 0 && ec.Scope == "99" && ec.Type == KnownEntityCounters.CustomerImages);
        var currentCustomerImageCount = customerImagesCounter.Next;
        var spaceImagesCounter = await dbContext.EntityCounters.SingleAsync(ec =>
            ec.Customer == 99 && ec.Scope == "1" && ec.Type == KnownEntityCounters.SpaceImages);
        var currentSpaceImagesCounter = spaceImagesCounter.Next;
        await dbContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.AsCustomer(99).DeleteAsync($"{assetId.ToApiResourcePath()}?deleteFrom=cdn,internalCache");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        // Asset, Location + Storage deleted
        var dbAsset = await dbContext.Images.SingleOrDefaultAsync(i => i.Id == assetId);
        dbAsset.Should().BeNull();
        var dbLocation = await dbContext.ImageLocations.SingleOrDefaultAsync(i => i.Id == assetId);
        dbLocation.Should().BeNull();
        
        var dbStorage = await dbContext.ImageStorages.SingleOrDefaultAsync(i => i.Id == assetId);
        dbStorage.Should().BeNull();
        
        // CustomerStorage values reduced
        await dbContext.Entry(customerSpaceStorage.Entity).ReloadAsync();
        customerSpaceStorage.Entity.NumberOfStoredImages.Should().Be(99);
        customerSpaceStorage.Entity.TotalSizeOfThumbnails.Should().Be(900L);
        customerSpaceStorage.Entity.TotalSizeOfStoredImages.Should().Be(600L);
        
        await dbContext.Entry(customerStorage.Entity).ReloadAsync();
        customerStorage.Entity.NumberOfStoredImages.Should().Be(199);
        customerStorage.Entity.TotalSizeOfThumbnails.Should().Be(1900L);
        customerStorage.Entity.TotalSizeOfStoredImages.Should().Be(1600L);
        
        // EntityCounter for customer images reduced
        var dbCustomerCounter = await dbContext.EntityCounters.SingleAsync(ec =>
            ec.Customer == 0 && ec.Scope == "99" && ec.Type == KnownEntityCounters.CustomerImages);
        dbCustomerCounter.Next.Should().Be(currentCustomerImageCount - 1);
        
        // EntityCounter for space images reduced
        var dbSpaceCounter = await dbContext.EntityCounters.SingleAsync(ec =>
            ec.Customer == 99 && ec.Scope == "1" && ec.Type == KnownEntityCounters.SpaceImages);
        dbSpaceCounter.Next.Should().Be(currentSpaceImagesCounter - 1);

        A.CallTo(() => NotificationSender.SendAssetModifiedMessage(
            A<AssetModificationRecord>.That.Matches(r => r.ChangeType == ChangeType.Delete && 
                                                         (int)r.DeleteFrom == 12), 
            A<CancellationToken>._)).MustHaveHappened();
    }
    
      [Fact]
    public async Task Delete_RemovesAssetWithoutImageLocation_FromDb()
    {
        // Arrange
        var assetId = new AssetId(99, 1, nameof(Delete_RemovesAssetAndAssociatedEntities_FromDb));
        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.ImageStorages.AddTestImageStorage(assetId, size: 400L, thumbSize: 0L);
        var customerSpaceStorage = await dbContext.CustomerStorages.AddTestCustomerStorage(space: 1, numberOfImages: 100,
            sizeOfStored: 1000L, sizeOfThumbs: 1000L);
        var customerStorage = await dbContext.CustomerStorages.AddTestCustomerStorage(space: 0, numberOfImages: 200,
            sizeOfStored: 2000L, sizeOfThumbs: 2000L);
        var customerImagesCounter = await dbContext.EntityCounters.SingleAsync(ec =>
            ec.Customer == 0 && ec.Scope == "99" && ec.Type == KnownEntityCounters.CustomerImages);
        var currentCustomerImageCount = customerImagesCounter.Next;
        var spaceImagesCounter = await dbContext.EntityCounters.SingleAsync(ec =>
            ec.Customer == 99 && ec.Scope == "1" && ec.Type == KnownEntityCounters.SpaceImages);
        var currentSpaceImagesCounter = spaceImagesCounter.Next;
        await dbContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.AsCustomer(99).DeleteAsync(assetId.ToApiResourcePath());
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        // Asset + Storage deleted
        var dbAsset = await dbContext.Images.SingleOrDefaultAsync(i => i.Id == assetId);
        dbAsset.Should().BeNull();
        var dbLocation = await dbContext.ImageLocations.SingleOrDefaultAsync(i => i.Id == assetId);
        dbLocation.Should().BeNull();

        // CustomerStorage values reduced
        await dbContext.Entry(customerSpaceStorage.Entity).ReloadAsync();
        customerSpaceStorage.Entity.NumberOfStoredImages.Should().Be(99);
        customerSpaceStorage.Entity.TotalSizeOfThumbnails.Should().Be(1000L);
        customerSpaceStorage.Entity.TotalSizeOfStoredImages.Should().Be(600L);
        
        await dbContext.Entry(customerStorage.Entity).ReloadAsync();
        customerStorage.Entity.NumberOfStoredImages.Should().Be(199);
        customerStorage.Entity.TotalSizeOfThumbnails.Should().Be(2000L);
        customerStorage.Entity.TotalSizeOfStoredImages.Should().Be(1600L);
        
        // EntityCounter for customer images reduced
        var dbCustomerCounter = await dbContext.EntityCounters.SingleAsync(ec =>
            ec.Customer == 0 && ec.Scope == "99" && ec.Type == KnownEntityCounters.CustomerImages);
        dbCustomerCounter.Next.Should().Be(currentCustomerImageCount - 1);
        
        // EntityCounter for space images reduced
        var dbSpaceCounter = await dbContext.EntityCounters.SingleAsync(ec =>
            ec.Customer == 99 && ec.Scope == "1" && ec.Type == KnownEntityCounters.SpaceImages);
        dbSpaceCounter.Next.Should().Be(currentSpaceImagesCounter - 1);
        
        A.CallTo(() => NotificationSender.SendAssetModifiedMessage(
            A<AssetModificationRecord>.That.Matches(r => r.ChangeType == ChangeType.Delete), 
            A<CancellationToken>._)).MustHaveHappened();
    }

    [Fact]
    public async Task Reingest_404_IfAssetNotFound()
    {
        // Arrange
        var assetId = new AssetId(99, 1, nameof(Reingest_404_IfAssetNotFound));
        var request = new HttpRequestMessage(HttpMethod.Post, $"{assetId.ToApiResourcePath()}/reingest");

        // Act
        var response = await httpClient.AsCustomer(99).SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Theory]
    [InlineData(AssetFamily.File)]
    [InlineData(AssetFamily.Timebased)]
    public async Task Reingest_400_IfNotImageFamily(AssetFamily family)
    {
        // Arrange
        var assetId = new AssetId(99, 1, $"{nameof(Reingest_400_IfNotImageFamily)}{family}");
        await dbContext.Images.AddTestAsset(assetId, family: family);
        await dbContext.SaveChangesAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, $"{assetId.ToApiResourcePath()}/reingest");

        // Act
        var response = await httpClient.AsCustomer(99).SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Reingest_Success_IfImageLocationDoesNotExist()
    {
        // Arrange
        var assetId = new AssetId(99, 1, nameof(Reingest_Success_IfImageLocationDoesNotExist));
        var asset = (await dbContext.Images.AddTestAsset(assetId, error: "Failed", ingesting: false)).Entity;
        await dbContext.SaveChangesAsync();
        
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), 
                    A<Asset>._, 
                    false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{assetId.ToApiResourcePath()}/reingest");

        // Act
        var response = await httpClient.AsCustomer(99).SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Engine called
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), 
                    A<Asset>._, 
                    false,
                    A<CancellationToken>._))
            .MustHaveHappened();

        var imageLocation = await dbContext.ImageLocations.SingleOrDefaultAsync(l => l.Id == assetId);
        imageLocation.Should().BeNull("API does not create ImageLocation record");

        await dbContext.Entry(asset).ReloadAsync();
        asset.Error.Should().BeNullOrEmpty();
        asset.Ingesting.Should().BeTrue();
    }
    
    [Fact]
    public async Task Reingest_Success_IfImageLocationExists()
    {
        // Arrange
        var assetId = new AssetId(99, 1, nameof(Reingest_Success_IfImageLocationExists));
        var asset = (await dbContext.Images.AddTestAsset(assetId, error: "Failed", ingesting: false)).Entity;
        await dbContext.ImageLocations.AddTestImageLocation(assetId, "s3://foo");
        await dbContext.SaveChangesAsync();
        
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), 
                    A<Asset>._, 
                    false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{assetId.ToApiResourcePath()}/reingest");

        // Act
        var response = await httpClient.AsCustomer(99).SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Engine called
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), 
                    A<Asset>._, 
                    false,
                    A<CancellationToken>._))
            .MustHaveHappened();

        var imageLocation = await dbContext.ImageLocations.SingleAsync(l => l.Id == assetId);
        imageLocation.Nas.Should().BeNullOrEmpty();
        imageLocation.S3.Should().Be("s3://foo", "ImageLocation should not be changed");

        await dbContext.Entry(asset).ReloadAsync();
        asset.Error.Should().BeNullOrEmpty();
        asset.Ingesting.Should().BeTrue();
    }
    
    [Fact]
    public async Task Reingest_ClearsBatchId_IfSet()
    {
        // Arrange
        var assetId = new AssetId(99, 1, nameof(Reingest_ClearsBatchId_IfSet));
        var asset = (await dbContext.Images.AddTestAsset(assetId, error: "Failed", ingesting: false, batch: 123))
            .Entity;
        await dbContext.ImageLocations.AddTestImageLocation(assetId);
        await dbContext.SaveChangesAsync();
        
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), 
                    A<Asset>._, 
                    false,
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{assetId.ToApiResourcePath()}/reingest");

        // Act
        var response = await httpClient.AsCustomer(99).SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Engine called
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId),
                    A<Asset>._, 
                    false,
                    A<CancellationToken>._))
            .MustHaveHappened();

        var imageLocation = await dbContext.ImageLocations.SingleAsync(l => l.Id == assetId);
        imageLocation.Nas.Should().BeNullOrEmpty();

        await dbContext.Entry(asset).ReloadAsync();
        asset.Error.Should().BeNullOrEmpty();
        asset.Batch.Should().Be(0);
        asset.Ingesting.Should().BeTrue();
    }
    
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadRequest, HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InsufficientStorage, HttpStatusCode.InsufficientStorage)]
    [InlineData(HttpStatusCode.GatewayTimeout, HttpStatusCode.InternalServerError)]
    public async Task Reingest_ReturnsAppropriateStatusCode_IfEngineFails(HttpStatusCode engine, HttpStatusCode api)
    {
        // Arrange
        var assetId = new AssetId(99, 1, $"{nameof(Reingest_ReturnsAppropriateStatusCode_IfEngineFails)}{engine}");
        await dbContext.Images.AddTestAsset(assetId, error: "Failed", ingesting: false);
        await dbContext.SaveChangesAsync();
        
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<IngestAssetRequest>.That.Matches(r => r.Id == assetId), A<Asset>._, false,
                    A<CancellationToken>._))
            .Returns(engine);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{assetId.ToApiResourcePath()}/reingest");

        // Act
        var response = await httpClient.AsCustomer(99).SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(api);
    }
    
    private byte[] StreamToBytes(Stream input)
    {
        using MemoryStream ms = new MemoryStream();
        input.CopyTo(ms);
        return ms.ToArray();
    }
}
