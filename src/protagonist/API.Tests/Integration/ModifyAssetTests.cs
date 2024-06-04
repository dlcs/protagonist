using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using Amazon.S3;
using API.Client;
using API.Infrastructure.Messaging;
using API.Tests.Integration.Infrastructure;
using DLCS.Core.Types;
using DLCS.HydraModel;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.Policies;
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
using Test.Helpers.Data;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;
using AssetFamily = DLCS.Model.Assets.AssetFamily;

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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
    public async Task Put_NewImageAsset_Creates_Asset_WithDeliveryChannelsSetToNone()
    {
        var customerAndSpace = await CreateCustomerAndSpace();

        var assetId = new AssetId(customerAndSpace.customer, customerAndSpace.space, nameof(Put_NewImageAsset_Creates_Asset_WithDeliveryChannelsSetToNone));
        var hydraImageBody = $@"{{
            ""@type"": ""Image"",
            ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
            ""family"": ""I"",
            ""mediaType"": ""image/tiff"",
            ""deliveryChannels"": [
            {{
                ""channel"": ""none"",
                ""policy"": ""none""
            }}]
        }}";
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
        asset.ImageDeliveryChannels
            .Should().HaveCount(1).And.Subject
            .Should().Satisfy(
                i => i.Channel == AssetDeliveryChannels.None &&
                     i.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.None);
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

        var assetId = new AssetId(customerAndSpace.customer, customerAndSpace.space, nameof(Put_NewImageAsset_Creates_Asset_WithCustomDefaultDeliveryChannel));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
  ""family"": ""I"",
  ""mediaType"": ""image/tiff""
}}";
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
    public async Task Put_NewImageAsset_Returns400_IfNoDeliveryChannelDefaults()
    {
        // Arrange
        var assetId = new AssetId(99, 1, nameof(Put_NewImageAsset_Returns400_IfNoDeliveryChannelDefaults));
        var hydraImageBody = @"{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/test"",
  ""family"": ""I"",
  ""mediaType"": ""image/tiff"",
  ""deliveryChannels"": [{ ""channel"":""file"" } ] 
}";
        
        // Act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer().PutAsync(assetId.ToApiResourcePath(), content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "there is no default handler for image/tiff for 'file' channel");
    }
    
    [Fact]
    public async Task Put_NewImageAsset_Creates_AssetWithSpecifiedDeliveryChannels()
    {
        var customerAndSpace = await CreateCustomerAndSpace();

        var assetId = new AssetId(customerAndSpace.customer, customerAndSpace.space, nameof(Put_NewImageAsset_Creates_AssetWithSpecifiedDeliveryChannels));
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
                                                                x.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ImageDefault);
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "thumbs");
    }

    [Fact]
    public async Task Put_NewImageAsset_BadRequest_WhenDeliveryChannelInvalid()
    {
        // arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var hydraImageBody = $@"{{
            ""@type"": ""Image"",
            ""origin"": ""https://example.org/my-image.png"",
            ""family"": ""I"",
            ""mediaType"": ""image/png"",
            ""deliveryChannels"": [
            {{
                ""channel"":""bad-delivery-channel"",
                ""policy"":""default""
            }}]
        }}";  
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Put_NewImageAsset_FailsToCreateAsset_WhenMediaTypeAndFamilyNotSet()
    {
        var assetId = new AssetId(99, 1, nameof(Put_NewImageAsset_FailsToCreateAsset_WhenMediaTypeAndFamilyNotSet));
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.tiff""
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
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Put_NewImageAsset_FailsToCreateAsset_WhenMatchingDefaultDeliveryChannelsAreInvalid()
    {
        // arrange
        const int customerId = 9901;
        var assetId = new AssetId(customerId, 1, nameof(Put_NewImageAsset_FailsToCreateAsset_WhenMatchingDefaultDeliveryChannelsAreInvalid));
        var hydraImageBody = $@"{{
          ""@type"": ""Image"",
          ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
          ""mediaType"": ""application/tiff""
        }}";

        await dbContext.Spaces.AddTestSpace(customerId, 1);
        
        dbContext.DefaultDeliveryChannels.Add(new DLCS.Model.DeliveryChannels.DefaultDeliveryChannel()
        {
            Customer = customerId,
            MediaType = "application/*",
            DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.FileNone,
        });
        dbContext.DefaultDeliveryChannels.Add(new DLCS.Model.DeliveryChannels.DefaultDeliveryChannel()
        {
            Customer = customerId,
            MediaType = "application/*",
            DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.None,
        });

        await dbContext.SaveChangesAsync();
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(assetId.ToApiResourcePath(), content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }  
    
    [Fact]
    public async Task Put_NewImageAsset_CreatesAsset_WhenMediaTypeAndFamilyNotSetWithLegacyEnabled()
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = new AssetId(customer, space, nameof(Put_NewImageAsset_CreatesAsset_WhenMediaTypeAndFamilyNotSetWithLegacyEnabled));
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
                                                                x.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ImageDefault);
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "thumbs");
    }
    
    [Fact]
    public async Task Put_NewImageAsset_CreatesAsset_WhenInferringOfMediaTypeNotPossibleWithLegacyEnabled()
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = new AssetId(customer, space, nameof(Put_NewImageAsset_CreatesAsset_WhenInferringOfMediaTypeNotPossibleWithLegacyEnabled));
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
                                                                x.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ImageDefault);
        asset.ImageDeliveryChannels.Should().ContainSingle(x => x.Channel == "thumbs" &&
                                                                x.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ThumbsDefault);
    }
    
    [Fact]
    public async Task Put_Existing_Asset_UpdatesAsset_IfIncomingDeliveryChannelsNull_AndLegacyEnabled()
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = AssetIdGenerator.GetAssetId(customer, space);
        
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Spaces.AddTestSpace(customer, space);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(customer);
        
        var expectedDeliveryChannels = new List<ImageDeliveryChannel>()
        {
            new()
            {
                Channel = AssetDeliveryChannels.File,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.FileNone
            }
        };
        
        var testAsset = await dbContext.Images.AddTestAsset(assetId, customer: customer, space: space, 
            origin: $"https://example.org/{assetId.Asset}.tiff", imageDeliveryChannels: expectedDeliveryChannels);
        
        await dbContext.SaveChangesAsync();
        
        var hydraImageBody = $@"{{
            ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
            ""string1"": ""my-string""
        }}";
        
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // Act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customer).PutAsync(assetId.ToApiResourcePath(), content);
       
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await dbContext.Entry(testAsset.Entity).ReloadAsync();
        testAsset.Entity.Reference1.Should().Be("my-string");
        testAsset.Entity.ImageDeliveryChannels.Should().BeEquivalentTo(expectedDeliveryChannels); // Should be unchanged
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
                A<Asset>.That.Matches(r => r.Id == assetId),
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
        await dbContext.SaveChangesAsync();
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.mp4"",
  ""family"": ""T"",
  ""mediaType"": ""audio/mp4""
}}";
        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
                                                                x.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.AvDefaultAudio);
    }
    
    [Fact]
    public async Task Put_NewVideoAsset_Creates_Asset()
    {
        var assetId = new AssetId(99, 1, nameof(Put_NewVideoAsset_Creates_Asset));
        await dbContext.SaveChangesAsync();
        var hydraImageBody = $@"{{
  ""@type"": ""Image"",
  ""origin"": ""https://example.org/{assetId.Asset}.mp4"",
  ""family"": ""T"",
  ""mediaType"": ""video/mp4""
}}";
        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
                                                                x.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.AvDefaultVideo);
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
    public async Task Put_Asset_Fails_When_ThumbnailPolicy_Is_Provided()
    {
        // Arrange 
        var assetId = new AssetId(99, 1, $"{nameof(Put_Asset_Fails_When_ThumbnailPolicy_Is_Provided)}");
        var hydraImageBody = $@"{{
          ""@type"": ""Image"",
          ""mediaType"":""image/tiff"",
          ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
          ""family"": ""I"",
          ""thumbnailPolicy"": ""thumbs-policy""
        }}";    
        
        // Act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("'thumbnailPolicy' is deprecated. Use 'deliveryChannels' instead.");
    }
    
    [Fact]
    public async Task Put_Asset_Fails_When_ImageOptimisationPolicy_Is_Provided()
    {
        // Arrange 
        var assetId = new AssetId(99, 1, $"{nameof(Put_Asset_Fails_When_ThumbnailPolicy_Is_Provided)}");
        var hydraImageBody = $@"{{
          ""@type"": ""Image"",
          ""mediaType"":""image/tiff"",
          ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
          ""family"": ""I"",
          ""deliveryChannels"": [""iiif-img""],
          ""imageOptimisationPolicy"": ""image-optimisation-policy""
        }}";    
        
        // Act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("'imageOptimisationPolicy' is deprecated. Use 'deliveryChannels' instead.");
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
            ""mediaType"": ""image/tiff"",
            ""deliveryChannels"": [
            {{
                ""channel"": ""iiif-img"",
                ""policy"": ""default""
            }}]
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        await dbContext.Entry(newAsset.Entity).ReloadAsync();
        newAsset.Entity.Ingesting.Should().BeTrue();
        newAsset.Entity.Error.Should().BeEmpty();
    }
    
    [Fact]
    public async Task Put_Existing_Asset_Returns400_IfDeliveryChannelsNull()
    {
        var assetId = new AssetId(99, 1, nameof(Put_Existing_Asset_Returns400_IfDeliveryChannelsNull));
        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();
        
        var hydraImageBody = $@"{{
            ""@type"": ""Image"",
            ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
            ""family"": ""I"",
            ""mediaType"": ""image/tiff""
        }}";
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Delivery channels are required when updating an existing Asset via PUT");
    }
    
    [Fact]
    public async Task Put_Existing_Asset_Returns400_IfDeliveryChannelsEmpty()
    {
        var assetId = new AssetId(99, 1, nameof(Put_Existing_Asset_Returns400_IfDeliveryChannelsEmpty));
        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();
        
        var hydraImageBody = $@"{{
            ""@type"": ""Image"",
            ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
            ""family"": ""I"",
            ""mediaType"": ""image/tiff"",
            ""deliveryChannels"": []
        }}";
        
        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Delivery channels are required when updating an existing Asset via PUT");
    }

    [Fact]
    public async Task Put_Existing_Asset_AllowsUpdatingDeliveryChannel()
    {
        // Arrange
        var assetId = new AssetId(99, 1, $"{nameof(Put_Existing_Asset_AllowsUpdatingDeliveryChannel)}");
        
        await dbContext.Images.AddTestAsset(assetId, imageDeliveryChannels: new List<ImageDeliveryChannel>
        {
            new()
            {
                Channel = AssetDeliveryChannels.Image,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault
            },
            new()
            {
                Channel = AssetDeliveryChannels.Thumbnails,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ThumbsDefault
            }
        });
        await dbContext.SaveChangesAsync();
        
        // change iiif-img to 'use-original', remove thumbs, add file
        var hydraImageBody = $@"{{
            ""@type"": ""Image"",
            ""origin"": ""https://example.org/{assetId.Asset}"",
            ""mediaType"": ""image/tiff"",
            ""deliveryChannels"": [
            {{
                ""channel"":""iiif-img"",
                ""policy"":""use-original""
            }},
            {{
                ""channel"":""file"",
                ""policy"":""none""
            }}]
        }}";
        
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // Act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels).Single(x => x.Id == assetId);
        asset.Id.Should().Be(assetId);
        asset.ImageDeliveryChannels
            .Should().HaveCount(2).And.Subject
            .Should().Satisfy(
                i => i.Channel == AssetDeliveryChannels.Image &&
                     i.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ImageUseOriginal,
                i => i.Channel == AssetDeliveryChannels.File);
    }

    [Fact]
    public async Task Put_Existing_Asset_AllowsSettingDeliveryChannelsToNone()
    {
        // Arrange
        var assetId = new AssetId(99, 1, $"{nameof(Put_Existing_Asset_AllowsSettingDeliveryChannelsToNone)}");
        
        await dbContext.Images.AddTestAsset(assetId, imageDeliveryChannels: new List<ImageDeliveryChannel>
        {
            new()
            {
                Channel = AssetDeliveryChannels.Image,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault
            },
            new()
            {
                Channel = AssetDeliveryChannels.Thumbnails,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ThumbsDefault
            }
        });
        await dbContext.SaveChangesAsync();
        
        var hydraImageBody = $@"{{
            ""@type"": ""Image"",
            ""origin"": ""https://example.org/{assetId.Asset}"",
            ""mediaType"": ""image/tiff"",
            ""deliveryChannels"": [
            {{
                ""channel"":""none"",
                ""policy"":""none""
            }}]
        }}";
        
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);
        
        // Act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels).Single(x => x.Id == assetId);
        asset.Id.Should().Be(assetId);
        asset.ImageDeliveryChannels
            .Should().HaveCount(1).And.Subject
            .Should().Satisfy(
                i => i.Channel == AssetDeliveryChannels.None &&
                     i.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.None);
    }
    
    [Fact]
    public async Task Put_Asset_Returns_InsufficientStorage_If_Policy_Exceeded()
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

        var assetId = new AssetId(customer, 1, nameof(Put_Asset_Returns_InsufficientStorage_If_Policy_Exceeded));
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
    [InlineData("fast-higher")]
    [InlineData("https://api.dlc.services/imageOptimisationPolicies/fast-higher")]
    public async Task Put_NewImageAsset_WithImageOptimisationPolicy_Creates_Asset_WhenLegacyEnabled(string imageOptimisationPolicy)
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = AssetIdGenerator.GetAssetId(customer, space);
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
        asset.ImageOptimisationPolicy.Should().BeEmpty();
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
        var assetId = AssetIdGenerator.GetAssetId(customer, space);
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
    [InlineData("fast-higher")]
    [InlineData("https://api.dlc.services/imageOptimisationPolicies/fast-higher")]
    public async Task Put_ExistingImageAsset_WithImageOptimisationPolicy_AddsDeliveryChannelsToAsset_WhenLegacyEnabled(string imageOptimisationPolicy)
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = AssetIdGenerator.GetAssetId(customer, space);
        
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Spaces.AddTestSpace(customer, space);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(customer);
        await dbContext.DeliveryChannelPolicies.AddTestDeliveryChannelPolicies(customer);
        await dbContext.Images.AddTestAsset(assetId, customer: customer, space: space,
            family: AssetFamily.Image, origin: "https://images.org/image.tiff", mediaType: "image/tiff",
            imageOptimisationPolicy: string.Empty, thumbnailPolicy: string.Empty);
        await dbContext.SaveChangesAsync();

        var hydraImageBody = $@"{{
          ""origin"": ""https://images.org/{assetId.Asset}.tiff"",
          ""imageOptimisationPolicy"" : ""{imageOptimisationPolicy}"",
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(i => i.DeliveryChannelPolicy).Single(i => i.Id == assetId);
        
        asset.MediaType.Should().Be("image/tiff");
        asset.Family.Should().Be(AssetFamily.Image);
        asset.ImageOptimisationPolicy.Should().BeEmpty();
        asset.ThumbnailPolicy.Should().BeEmpty();
        asset.ImageDeliveryChannels.Count.Should().Be(2);
        asset.ImageDeliveryChannels.Should().ContainSingle(dc => dc.Channel == AssetDeliveryChannels.Image &&
                                                                            dc.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ImageDefault);
        asset.ImageDeliveryChannels.Should().ContainSingle(dc => dc.Channel == AssetDeliveryChannels.Thumbnails &&
                                                                            dc.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ThumbsDefault);
    }
    
    [Theory]
    [InlineData("default")]
    [InlineData("https://api.dlc.services/thumbnailPolicies/default")]
    public async Task Put_NewImageAsset_WithThumbnailPolicy_Creates_Asset_WhenLegacyEnabled(string thumbnailPolicy)
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = AssetIdGenerator.GetAssetId(customer, space);

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
        asset.ImageOptimisationPolicy.Should().BeEmpty();
        asset.ThumbnailPolicy.Should().BeEmpty();
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
        var assetId = AssetIdGenerator.GetAssetId(customer, space);
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
    [InlineData("default")]
    [InlineData("https://api.dlc.services/thumbnailPolicies/default")]
    public async Task Put_ExistingImageAsset_WithThumbnailPolicy_AddsDeliveryChannelsToAsset_WhenLegacyEnabled(string thumbnailPolicy)
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = AssetIdGenerator.GetAssetId(customer, space);
        
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Spaces.AddTestSpace(customer, space);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(customer);
        await dbContext.DeliveryChannelPolicies.AddTestDeliveryChannelPolicies(customer);
        await dbContext.Images.AddTestAsset(assetId, customer: customer, space: space,
            family: AssetFamily.Image, origin: "https://images.org/image.tiff", mediaType: "image/tiff",
            imageOptimisationPolicy: string.Empty, thumbnailPolicy: string.Empty);
        await dbContext.SaveChangesAsync();

        var hydraImageBody = $@"{{
          ""origin"": ""https://images.org/{assetId.Asset}.tiff"",
          ""thumbnailPolicy"" : ""{thumbnailPolicy}"",
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(i => i.DeliveryChannelPolicy).Single(i => i.Id == assetId);
        asset.MediaType.Should().Be("image/tiff");
        asset.Family.Should().Be(AssetFamily.Image);
        asset.ImageOptimisationPolicy.Should().BeEmpty();
        asset.ThumbnailPolicy.Should().BeEmpty();
        asset.ImageDeliveryChannels.Count.Should().Be(2);
        asset.ImageDeliveryChannels.Should().ContainSingle(dc => dc.Channel == AssetDeliveryChannels.Image &&
                                                                            dc.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ImageDefault);
        asset.ImageDeliveryChannels.Should().ContainSingle(dc => dc.Channel == AssetDeliveryChannels.Thumbnails &&
                                                                            dc.DeliveryChannelPolicy.Name == "default");
    }
    
    [Theory]
    [InlineData("video-max")]
    [InlineData("https://api.dlc.services/imageOptimisationPolicy/video-max")]
    public async Task Put_NewVideoAsset_WithImageOptimisationPolicy_Creates_Asset_WhenLegacyEnabled(string imageOptimisationPolicy)
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = AssetIdGenerator.GetAssetId(customer, space);
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
        asset.ImageOptimisationPolicy.Should().BeEmpty();
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
        var assetId = AssetIdGenerator.GetAssetId(customer, space);
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
    [InlineData("video-max")]
    [InlineData("https://api.dlc.services/imageOptimisationPolicies/video-max")]
    public async Task Put_ExistingVideoAsset_WithImageOptimisationPolicy_AddsDeliveryChannelsToAsset_WhenLegacyEnabled(string imageOptimisationPolicy)
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = AssetIdGenerator.GetAssetId(customer, space);
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Spaces.AddTestSpace(customer, space);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(customer);
        await dbContext.DeliveryChannelPolicies.AddTestDeliveryChannelPolicies(customer);
        await dbContext.Images.AddTestAsset(assetId, customer: customer, space: space,
            family: AssetFamily.Timebased, origin: "https://images.org/image.mp4", mediaType: "video/mp4",
            imageOptimisationPolicy: string.Empty, thumbnailPolicy: string.Empty);
        await dbContext.SaveChangesAsync();

        var hydraImageBody = $@"{{
          ""family"": ""T"",
          ""origin"": ""https://images.org/{assetId.Asset}.mp4"",
          ""thumbnailPolicy"" : ""{imageOptimisationPolicy}"",
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(i => i.DeliveryChannelPolicy).Single(i => i.Id == assetId);
        asset.MediaType.Should().Be("video/mp4");
        asset.Family.Should().Be(AssetFamily.Timebased);
        asset.ImageOptimisationPolicy.Should().BeEmpty();
        asset.ThumbnailPolicy.Should().BeEmpty();
        asset.ImageDeliveryChannels.Count.Should().Be(1);
        asset.ImageDeliveryChannels.Should().ContainSingle(dc => dc.Channel == AssetDeliveryChannels.Timebased &&
                                                                            dc.DeliveryChannelPolicy.Name == "default-video");
    }
    
    [Theory]
    [InlineData("audio-max")]
    [InlineData("https://api.dlc.services/imageOptimisationPolicy/audio-max")]
    public async Task Put_NewAudioAsset_WithImageOptimisationPolicy_Creates_Asset_WhenLegacyEnabled(string imageOptimisationPolicy)
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = AssetIdGenerator.GetAssetId(customer, space);
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
        asset.ImageOptimisationPolicy.Should().BeEmpty();
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
        var assetId = AssetIdGenerator.GetAssetId(customer, space);
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
    
    [Theory]
    [InlineData("audio-max")]
    [InlineData("https://api.dlc.services/imageOptimisationPolicies/audio-max")]
    public async Task Put_ExistingAudioAsset_WithImageOptimisationPolicy_AddsDeliveryChannelsToAsset_WhenLegacyEnabled(string imageOptimisationPolicy)
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = AssetIdGenerator.GetAssetId(customer, space);
        await dbContext.Customers.AddTestCustomer(customer);
        await dbContext.Spaces.AddTestSpace(customer, space);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(customer);
        await dbContext.DeliveryChannelPolicies.AddTestDeliveryChannelPolicies(customer);
        await dbContext.Images.AddTestAsset(assetId, customer: customer, space: space,
            family: AssetFamily.Timebased, origin: "https://images.org/image.mp3", mediaType: "audio/mp3",
            imageOptimisationPolicy: string.Empty, thumbnailPolicy: string.Empty);
        await dbContext.SaveChangesAsync();

        var hydraImageBody = $@"{{
          ""family"": ""T"",
          ""origin"": ""https://images.org/{assetId.Asset}.mp3"",
          ""thumbnailPolicy"" : ""{imageOptimisationPolicy}"",
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .ThenInclude(i => i.DeliveryChannelPolicy).Single(i => i.Id == assetId);
        asset.MediaType.Should().Be("audio/mp3");
        asset.Family.Should().Be(AssetFamily.Timebased);
        asset.ImageOptimisationPolicy.Should().BeEmpty();
        asset.ThumbnailPolicy.Should().BeEmpty();
        asset.ImageDeliveryChannels.Count.Should().Be(1);
        asset.ImageDeliveryChannels.Should().ContainSingle(dc => dc.Channel == AssetDeliveryChannels.Timebased &&
                                                                            dc.DeliveryChannelPolicy.Name == "default-audio");
    }
    
    [Fact]
    public async Task Put_NewFileAsset_Creates_Asset_WhenLegacyEnabled()
    {
        const int customer = 325665;
        const int space = 2;
        var assetId = AssetIdGenerator.GetAssetId(customer, space);
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .MustNotHaveHappened();
        
        await dbContext.Entry(testAsset.Entity).ReloadAsync();
        testAsset.Entity.Reference1.Should().Be("I am edited");
    }
    
    [Fact]
    public async Task Patch_Asset_Leaves_ImageDeliveryChannels_Intact_WhenDeliveryChannelsNull()
    {
        // Arrange 
        var assetId = new AssetId(99, 1, 
            $"{nameof(Patch_Asset_Leaves_ImageDeliveryChannels_Intact_WhenDeliveryChannelsNull)}");
        
        await dbContext.Images.AddTestAsset(assetId, customer: 99, space: 1, family: AssetFamily.Image, 
            origin: "https://files.org/example.jpeg", imageDeliveryChannels: new List<ImageDeliveryChannel>()
            {
                new()
                {
                    Channel = AssetDeliveryChannels.File,
                    DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.FileNone,
                },
                new()
                {
                    Channel = AssetDeliveryChannels.Image,
                    DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault,
                },
                new()
                {
                    Channel = AssetDeliveryChannels.Thumbnails,
                    DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ThumbsDefault,
                },               
            });
        await dbContext.SaveChangesAsync();
        
        const string hydraImageBody = @"{
            ""mediaType"":""application/pdf"",
            ""tags"": [""my-tag""]
        }";    
        
        // Act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .Single(x => x.Id == assetId);
        asset.ImageDeliveryChannels.Should().Satisfy(
            dc => dc.Channel == AssetDeliveryChannels.File 
                  && dc.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.FileNone,
            dc => dc.Channel == AssetDeliveryChannels.Image
                  && dc.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ImageDefault,
            dc => dc.Channel == AssetDeliveryChannels.Thumbnails
                  && dc.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ThumbsDefault);
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);

        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .MustHaveHappened();
        
        await dbContext.Entry(testAsset.Entity).ReloadAsync();
        testAsset.Entity.Reference1.Should().Be("I am edited");
    }
    
    [Fact]
    public async Task Patch_ImageAsset_AllowsSettingDeliveryChannelsToNone()
    {
        var assetId = new AssetId(99, 1, nameof(Patch_ImageAsset_AllowsSettingDeliveryChannelsToNone));
        await dbContext.Images.AddTestAsset(assetId,
            imageDeliveryChannels: new List<ImageDeliveryChannel>
            {
                new()
                {
                    Channel = AssetDeliveryChannels.Image,
                    DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault
                },
                new()
                {
                    Channel = AssetDeliveryChannels.Thumbnails,
                    DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ThumbsDefault
                }
            });
        
        await dbContext.SaveChangesAsync();
        
        var hydraImageBody = $@"{{
            ""@type"": ""Image"",
            ""deliveryChannels"": [
            {{
                ""channel"":""none"",
                ""policy"":""none""
            }}]
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        A.CallTo(() =>
                EngineClient.SynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .MustHaveHappened();

        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels).Single(x => x.Id == assetId);
        asset.Id.Should().Be(assetId);
        asset.ImageDeliveryChannels
            .Should().HaveCount(1).And.Subject
            .Should().Satisfy(
                i => i.Channel == AssetDeliveryChannels.None &&
                     i.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.None);
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .Returns(true);

        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        A.CallTo(() =>
                EngineClient.AsynchronousIngest(
                    A<Asset>.That.Matches(r => r.Id == assetId),
                    A<CancellationToken>._))
            .MustHaveHappened();
        
        await dbContext.Entry(testAsset.Entity).ReloadAsync();
        testAsset.Entity.Reference1.Should().Be("I am edited");
        testAsset.Entity.Batch.Should().BeGreaterThan(0);
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
    public async Task Patch_Asset_Returns_BadRequest_if_DeliveryChannels_Empty()
    {
        // arrange
        var assetId = new AssetId(99, 1, nameof(Patch_Asset_Returns_BadRequest_if_DeliveryChannels_Empty));
        var hydraImageBody = $@"{{
          ""@type"": ""Image"",
          ""deliveryChannels"": []
        }}";

        // act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Patch_Asset_Fails_When_ThumbnailPolicy_Is_Provided()
    {
        // Arrange 
        var assetId = new AssetId(99, 1, $"{nameof(Patch_Asset_Fails_When_ThumbnailPolicy_Is_Provided)}");
        var hydraImageBody = $@"{{
          ""thumbnailPolicy"": ""thumbs-policy""
        }}";    
        
        // Act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("'thumbnailPolicy' is deprecated. Use 'deliveryChannels' instead.");
    }
    
    [Fact]
    public async Task Patch_Asset_Fails_When_ImageOptimisationPolicy_Is_Provided()
    {
        // Arrange 
        var assetId = new AssetId(99, 1, $"{nameof(Patch_Asset_Fails_When_ThumbnailPolicy_Is_Provided)}");
        var hydraImageBody = $@"{{
          ""imageOptimisationPolicy"": ""image-optimisation-policy""
        }}";    
        
        // Act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("'imageOptimisationPolicy' is deprecated. Use 'deliveryChannels' instead.");
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
    public async Task Patch_Images_Leaves_ImageDeliveryChannels_Intact_WhenDeliveryChannelsNull()
    {
        // Arrange
        await dbContext.Spaces.AddTestSpace(99, 3004);
        
        var assetId = AssetId.FromString($"99/3003/{nameof(Patch_Images_Leaves_ImageDeliveryChannels_Intact_WhenDeliveryChannelsNull)}");
        
        await dbContext.Images.AddTestAsset(assetId, customer: 99, space: 3004, family: AssetFamily.Image, 
            origin: "https://files.org/example.jpeg", imageDeliveryChannels: new List<ImageDeliveryChannel>() 
        {
            new()
            {
                Channel = AssetDeliveryChannels.File,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.FileNone,
            },
            new()
            {
                Channel = AssetDeliveryChannels.Image,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault,
            },
            new()
            {
                Channel = AssetDeliveryChannels.Thumbnails,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ThumbsDefault,
            },               
        });
        await dbContext.SaveChangesAsync();
        
        var hydraCollectionBody = $@"{{
          ""@type"": ""Collection"",
          ""member"": [
           {{
                ""@type"": ""Image"",
                ""id"": ""{assetId}"",
                ""tags"": [""my-tag""]
           }}]
        }}";
                
        // Act
        var content = new StringContent(hydraCollectionBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync("/customers/99/spaces/3004/images", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var asset = dbContext.Images.Include(i => i.ImageDeliveryChannels)
            .Single(x => x.Id == assetId);
        asset.ImageDeliveryChannels.Should().Satisfy(
            dc => dc.Channel == AssetDeliveryChannels.File 
                  && dc.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.FileNone,
            dc => dc.Channel == AssetDeliveryChannels.Image
                  && dc.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ImageDefault,
            dc => dc.Channel == AssetDeliveryChannels.Thumbnails
                  && dc.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ThumbsDefault);
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
    public async Task Delete_IncludesImageDeliveryChannels_InAssetModifiedMessage()
    {
        // Arrange
        var assetId = new AssetId(99, 1, nameof(Delete_IncludesImageDeliveryChannels_InAssetModifiedMessage));
        await dbContext.Images.AddTestAsset(assetId, imageDeliveryChannels: new List<ImageDeliveryChannel>()
        {
            new()
            {
                Channel = AssetDeliveryChannels.Image,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault
            },
            new()
            {
                Channel = AssetDeliveryChannels.Thumbnails,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ThumbsDefault
            },
            new()
            {
                Channel = AssetDeliveryChannels.File,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.FileNone
            }   
        });
        await dbContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.AsCustomer(99).DeleteAsync(assetId.ToApiResourcePath());
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        A.CallTo(() => NotificationSender.SendAssetModifiedMessage(
            A<AssetModificationRecord>.That.Matches(r => 
                r.ChangeType == ChangeType.Delete &&
                r.Before.ImageDeliveryChannels.Count == 3),
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
                    A<Asset>.That.Matches(r => r.Id == assetId),
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
