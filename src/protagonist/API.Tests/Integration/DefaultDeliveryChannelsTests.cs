using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.HydraModel;
using DLCS.Repository;
using Hydra.Collections;
using Hydra.Model;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class DefaultDeliveryChannelsTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    private readonly DlcsContext dlcsContext;

    public DefaultDeliveryChannelsTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
    {
        dlcsContext = dbFixture.DbContext;
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(dbFixture, "API-Test");
        dbFixture.CleanUp();
    }

    [Fact]
    public async Task Get_RetrieveAllDefaultDeliveryChannelsForCustomer_200()
    {
        // Arrange
        const int customerId = 1;
        var path = $"customers/{customerId}/defaultDeliveryChannels";

        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);
        var data = await response.ReadAsHydraResponseAsync<HydraCollection<DefaultDeliveryChannel>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        data.Members.Count(d => d.MediaType == "image/*").Should().Be(2);
    }
    
    [Fact]
    public async Task Get_RetrieveADefaultDeliveryChannelForCustomer_200()
    {
        // Arrange
        const int customerId = 1;
        var mediaType = "audio/*";

        var defaultDeliveryChannel = dlcsContext.DefaultDeliveryChannels.First(d => d.Customer == customerId && d.MediaType == mediaType);
        
        var path = $"customers/{customerId}/defaultDeliveryChannels/{defaultDeliveryChannel.Id}";

        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);
        var data = await response.ReadAsHydraResponseAsync<DefaultDeliveryChannel>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        data.MediaType.Should().Be(mediaType);
        data.Id.Should().Be($"{httpClient.BaseAddress}customers/{customerId}/defaultDeliveryChannels/{defaultDeliveryChannel.Id}");
    }
    
    [Fact]
    public async Task Get_RetrieveADefaultDeliveryChannelForDifferentCustomer_404()
    {
        // Arrange
        const int defaultCustomer = 1;
        
        const string newCustomerJson = @"{
  ""@type"": ""Customer"",
  ""name"": ""api-test-customer-default"",
  ""displayName"": ""My New Customer""
}";
        var customerContent = new StringContent(newCustomerJson, Encoding.UTF8, "application/json");
        var customerResponse = await httpClient.AsAdmin().PostAsync("/customers", customerContent);
        var customerData = await customerResponse.ReadAsHydraResponseAsync<Customer>();
        var customerId = int.Parse(customerData.Id!.Split('/').Last());
        var mediaType = "audio/*";

        var defaultDeliveryChannel = dlcsContext.DefaultDeliveryChannels.First(d => d.Customer == defaultCustomer && d.MediaType == mediaType);
        
        var path = $"customers/{customerId}/defaultDeliveryChannels/{defaultDeliveryChannel.Id}";

        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_RetrieveANonExistentDefaultDeliveryChannelForCustomer_404()
    {
        // Arrange
        const int customerId = 1;

        var path = $"customers/{customerId}/defaultDeliveryChannels/{Guid.Empty}";

        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_RetrieveANonGuidDefaultDeliveryChannelForCustomer_400()
    {
        // Arrange
        const int customerId = 1;

        var path = $"customers/{customerId}/defaultDeliveryChannels/notAGuid";

        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [InlineData("audio/mp3", "https://api.dlcs.io/customers/2/deliveryChannelPolicies/iiif-av/default-audio", "iiif-av")]
    [InlineData("video/mp4", "default-video", "iiif-av")]
    [InlineData("image/tiff", "default", "iiif-img")]
    public async Task Post_CreateDefaultDeliveryChannelForCustomer_201(string mediaType, string policyName, string channel)
    {
        // Arrange
        const string newCustomerJson = @"{
  ""@type"": ""Customer"",
  ""name"": ""api-test-customer-2"",
  ""displayName"": ""My New Customer""
}";
        var customerContent = new StringContent(newCustomerJson, Encoding.UTF8, "application/json");
        
        var customerResponse = await httpClient.AsAdmin().PostAsync("/customers", customerContent);

        var customerData = await customerResponse.ReadAsHydraResponseAsync<Customer>();

        var customerId = int.Parse(customerData.Id!.Split('/').Last());
        
        var path = $"customers/{customerId}/defaultDeliveryChannels";

        string newDefaultDeliveryChannelJson = JsonConvert.SerializeObject(new DefaultDeliveryChannel()
        {
            MediaType = mediaType,
            Policy = policyName,
            Channel = channel
        });
        
        // Act
        var content = new StringContent(newDefaultDeliveryChannelJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
        var data = await response.ReadAsHydraResponseAsync<DefaultDeliveryChannel>();

        var dbEntry =
            dlcsContext.DefaultDeliveryChannels.Single(d => d.Customer == customerId && d.MediaType == mediaType);

        var policy =
            dlcsContext.DeliveryChannelPolicies.SingleOrDefault(p =>
                p.Customer == customerId && p.Channel == channel && 
                p.Name == policyName.Split('/', StringSplitOptions.None).Last()) ??             
            dlcsContext.DeliveryChannelPolicies.Single(p =>
                p.Customer == 1 && p.Channel == channel && p.Name == policyName);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        data.MediaType.Should().Be(mediaType);
        data.Id.Should().Be($"{httpClient.BaseAddress}customers/{customerId}/defaultDeliveryChannels/{dbEntry.Id}");
        dbEntry.DeliveryChannelPolicyId.Should().Be(policy.Id);
    }
    
    [Theory]
    [InlineData(null, "default-audio", "iiif-av", null)]
    [InlineData("video/mp4", null, "iiif-av", null)]
    [InlineData("image/tiff", "default", null, null)]
    [InlineData("image/tiff", "default", "iiif-img", "7809693e-7d2c-45be-8943-af7732571a51")] // random guid
    public async Task Post_CreateDefaultDeliveryChannelForCustomerFailsValidation_400(string mediaType, string name, string channel, string id)
    {
        // Arrange
        const int customerId = 1;
        var path = $"customers/{customerId}/defaultDeliveryChannels";

        string newDefaultDeliveryChannelJson = JsonConvert.SerializeObject(new DefaultDeliveryChannel()
        {
            MediaType = mediaType,
            Policy = name,
            Channel = channel,
            Id = id
        });
        
        // Act
        var content = new StringContent(newDefaultDeliveryChannelJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Post_CreateDefaultDeliveryChannelForCustomerNonExistentPolicy_400()
    {
        // Arrange
        const int customerId = 1;
        var path = $"customers/{customerId}/defaultDeliveryChannels";

        string newDefaultDeliveryChannelJson = JsonConvert.SerializeObject(new DefaultDeliveryChannel()
        {
            MediaType = "some/value",
            Policy = "not-a-policy",
            Channel = "iiif-av"
        });
        
        // Act
        var content = new StringContent(newDefaultDeliveryChannelJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        var data = JsonConvert.DeserializeObject<Error>(await response.Content.ReadAsStringAsync());
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        data.Description.Should().Be("Failed to find linked delivery channel policy");
    }
    
    [Fact]
    public async Task Post_CreateDefaultDeliveryChannelForCustomerNonExistentPolicy_NotAChannel_400()
    {
        // Arrange
        const int customerId = 1;
        var path = $"customers/{customerId}/defaultDeliveryChannels";

        string newDefaultDeliveryChannelJson = JsonConvert.SerializeObject(new DefaultDeliveryChannel()
        {
            MediaType = "some/value",
            Policy = "not-a-policy",
            Channel = "not-a-channel"
        });
        
        // Act
        var content = new StringContent(newDefaultDeliveryChannelJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        var data = JsonConvert.DeserializeObject<Error>(await response.Content.ReadAsStringAsync());
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        data.Description.Should().Be("delivery channel not-a-channel is not a valid delivery channel");
    }
    
    [Fact]
    public async Task Post_CreateDefaultDeliveryChannelWhichAlreadyExists_409()
    {
        // Arrange
        const int customerId = 1;
        var path = $"customers/{customerId}/defaultDeliveryChannels";

        string newDefaultDeliveryChannelJson = JsonConvert.SerializeObject(new DefaultDeliveryChannel()
        {
            MediaType = "image/*",
            Policy = "default",
            Channel = "iiif-img"
        });
        
        // Act
        var content = new StringContent(newDefaultDeliveryChannelJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        var data = JsonConvert.DeserializeObject<Error>(await response.Content.ReadAsStringAsync());
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        data.Description.Should().Be("A default delivery channel for the requested media type 'image/*' already exists");
    }
    
    [Theory]
    [InlineData("audio/mp3", "audio/*", "https://api.dlcs.io/customers/2/deliveryChannelPolicies/iiif-av/default-audio", "iiif-av")]
    [InlineData("video/mp4", "video/*", "default-video", "iiif-av")]
    [InlineData("image/tiff", "image/*", "default", "iiif-img")]
    [InlineData("image/*", "image/*", "use-original", "iiif-img")]
    public async Task Put_UpdatesDefaultDeliveryChannelForCustomer_200(string mediaType, string initialMediaType, string policyName, string channel)
    {
        // Arrange
        const string newCustomerJson = @"{
  ""@type"": ""Customer"",
  ""name"": ""api-test-customer-3"",
  ""displayName"": ""My New Customer""
}";
        var customerContent = new StringContent(newCustomerJson, Encoding.UTF8, "application/json");
        
        var customerResponse = await httpClient.AsAdmin().PostAsync("/customers", customerContent);

        var customerData = await customerResponse.ReadAsHydraResponseAsync<Customer>();

        var customerId = int.Parse(customerData.Id!.Split('/').Last());
        
        var dbEntry =
            dlcsContext.DefaultDeliveryChannels.Single(d => d.Customer == customerId && 
                                                            d.MediaType == initialMediaType && d.DeliveryChannelPolicy.Channel == channel);
        var path = $"customers/{customerId}/defaultDeliveryChannels/{dbEntry.Id}";

        string newDefaultDeliveryChannelJson = JsonConvert.SerializeObject(new DefaultDeliveryChannel()
        {
            MediaType = mediaType,
            Policy = policyName,
            Channel = channel
        });
        
        // Act
        var content = new StringContent(newDefaultDeliveryChannelJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        // Assert
        var data = await response.ReadAsHydraResponseAsync<DefaultDeliveryChannel>();
        
        var policy =
            dlcsContext.DeliveryChannelPolicies.SingleOrDefault(p =>
                p.Customer == customerId && p.Channel == channel && 
                p.Name == policyName.Split("/", StringSplitOptions.None).Last()) ?? 
            dlcsContext.DeliveryChannelPolicies.Single(p =>
                p.Customer == 1 && p.Channel == channel && p.Name == policyName);
        
        var modifiedDbEntry =
            dlcsContext.DefaultDeliveryChannels .Include(d => d.DeliveryChannelPolicy)
                .Single(d => d.Customer == customerId && 
                             d.MediaType == mediaType && d.DeliveryChannelPolicy.Channel == channel);
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        data.MediaType.Should().Be(mediaType);
        data.Id.Should().Be($"{httpClient.BaseAddress}customers/{customerId}/defaultDeliveryChannels/{dbEntry.Id}");
        modifiedDbEntry.DeliveryChannelPolicy.Name.Should().Be(policyName.Split("/", StringSplitOptions.None).Last());
    }
    
    [Theory]
    [InlineData(null, "default-audio", "iiif-av")]
    [InlineData("video/mp4", null, "iiif-av")]
    [InlineData("image/tiff", "default", null)]
    public async Task Put_UpdateDefaultDeliveryChannelForCustomerFailsValidation_400(string mediaType, string policyName, string channel)
    {
        // Arrange
        const int customerId = 1;
        var path = $"customers/{customerId}/defaultDeliveryChannels/{Guid.NewGuid()}";

        string newDefaultDeliveryChannelJson = JsonConvert.SerializeObject(new DefaultDeliveryChannel()
        {
            MediaType = mediaType,
            Policy = policyName,
            Channel = channel
        });
        
        // Act
        var content = new StringContent(newDefaultDeliveryChannelJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Put_UpdateNonExistentDefaultDeliveryChannelForCustomerFails_InvalidChannel_400()
    {
        // Arrange
        const int customerId = 1;
        var path = $"customers/{customerId}/defaultDeliveryChannels/{Guid.NewGuid()}";

        string newDefaultDeliveryChannelJson = JsonConvert.SerializeObject(new DefaultDeliveryChannel()
        {
            MediaType = "mediaType",
            Policy = "policyName",
            Channel = "channel"
        });
        
        // Act
        var content = new StringContent(newDefaultDeliveryChannelJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Put_UpdateNonExistentDefaultDeliveryChannelForCustomerFails_DefaultDeliveryChannelNotFound_404()
    {
        // Arrange
        const int customerId = 1;
        var path = $"customers/{customerId}/defaultDeliveryChannels/{Guid.NewGuid()}";

        string newDefaultDeliveryChannelJson = JsonConvert.SerializeObject(new DefaultDeliveryChannel()
        {
            MediaType = "mediaType",
            Policy = "policyName",
            Channel = "iiif-img"
        });
        
        // Act
        var content = new StringContent(newDefaultDeliveryChannelJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_UpdateDefaultDeliveryChannelForCustomerFailsToFindPolicy_InvalidPolicy_400()
    {
        // Arrange
        const int customerId = 1;

        var policy =
            dlcsContext.DefaultDeliveryChannels.First(p =>
                p.Customer == customerId);
        
        var path = $"customers/{customerId}/defaultDeliveryChannels/{policy.Id}";

        string newDefaultDeliveryChannelJson = JsonConvert.SerializeObject(new DefaultDeliveryChannel()
        {
            MediaType = "some/value",
            Policy = "not-a-policy",
            Channel = "iiif-av"
        });
        
        // Act
        var content = new StringContent(newDefaultDeliveryChannelJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        var data = JsonConvert.DeserializeObject<Error>(await response.Content.ReadAsStringAsync());
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        data.Description.Should().Be("Failed to find linked delivery channel policy");
    }
    
    [Fact]
    public async Task Put_UpdateDefaultDeliveryChannelForCustomerFailsToFindPolicy_InvalidChannel_400()
    {
        // Arrange
        const int customerId = 1;

        var policy =
            dlcsContext.DefaultDeliveryChannels.First(p =>
                p.Customer == customerId);
        
        var path = $"customers/{customerId}/defaultDeliveryChannels/{policy.Id}";

        string newDefaultDeliveryChannelJson = JsonConvert.SerializeObject(new DefaultDeliveryChannel()
        {
            MediaType = "some/value",
            Policy = "not-a-policy",
            Channel = "not-a-channel"
        });
        
        // Act
        var content = new StringContent(newDefaultDeliveryChannelJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        var data = JsonConvert.DeserializeObject<Error>(await response.Content.ReadAsStringAsync());
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        data.Description.Should().Be("delivery channel not-a-channel is not a valid delivery channel");
    }
    
    [Fact]
    public async Task Put_UpdateANonExistentDefaultDeliveryChannelForCustomer_404()
    {
        // Arrange
        const int customerId = 1;

        var path = $"customers/{customerId}/defaultDeliveryChannels/{Guid.Empty}";
        string newDefaultDeliveryChannelJson = JsonConvert.SerializeObject(new DefaultDeliveryChannel()
        {
            MediaType = "whoCares",
            Policy = "whoCares",
            Channel = "iiif-img"
        });
        
        // Act
        var content = new StringContent(newDefaultDeliveryChannelJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Delete_DeleteADefaultDeliveryChannelForCustomer_200()
    {
        // Arrange
        const string newCustomerJson = @"{
  ""@type"": ""Customer"",
  ""name"": ""api-test-customer-4"",
  ""displayName"": ""My New Customer""
}";
        var customerContent = new StringContent(newCustomerJson, Encoding.UTF8, "application/json");
        
        var customerResponse = await httpClient.AsAdmin().PostAsync("/customers", customerContent);

        var customerData = await customerResponse.ReadAsHydraResponseAsync<Customer>();

        var customerId = int.Parse(customerData.Id!.Split('/').Last());
        
        var mediaType = "audio/*";

        var defaultDeliveryChannel = dlcsContext.DefaultDeliveryChannels.First(d => d.Customer == customerId && d.MediaType == mediaType);
        var path = $"customers/{customerId}/defaultDeliveryChannels/{defaultDeliveryChannel.Id}";

        // Act
        var response = await httpClient.AsCustomer(customerId).DeleteAsync(path);

        var defaultDeliveryChannelAfterDelete = dlcsContext.DefaultDeliveryChannels.FirstOrDefault(d => d.Customer == customerId && d.MediaType == mediaType);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        defaultDeliveryChannelAfterDelete.Should().BeNull();
    }
    
    [Fact]
    public async Task Delete_DeleteANonExistentDefaultDeliveryChannelForCustomer_404()
    {
        // Arrange
        const int customerId = 1;

        var path = $"customers/{customerId}/defaultDeliveryChannels/{Guid.Empty}";

        // Act
        var response = await httpClient.AsCustomer(customerId).DeleteAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_RetrieveAllDefaultDeliveryChannelsForCustomerAndSpace_DoesNotRetrieveDevaultValues_200()
    {
        // Arrange
        const int customerId = 1;
        const int space = 5;
        var path = $"customers/{customerId}/spaces/{space}/defaultDeliveryChannels";

        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);
        var data = await response.ReadAsHydraResponseAsync<HydraCollection<DefaultDeliveryChannel>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        data.Members.Count().Should().Be(0);
    }
    
    [Fact]
    public async Task Get_RetrieveADefaultDeliveryChannelForCustomerWithSpace_200()
    {
        // Arrange
        const string newCustomerJson = @"{
  ""@type"": ""Customer"",
  ""name"": ""api-test-customer-space-2"",
  ""displayName"": ""My New Customer""
}";
        var customerContent = new StringContent(newCustomerJson, Encoding.UTF8, "application/json");
        var customerResponse = await httpClient.AsAdmin().PostAsync("/customers", customerContent);
        var customerData = await customerResponse.ReadAsHydraResponseAsync<Customer>();
        var customerId = int.Parse(customerData.Id!.Split('/').Last());
        var mediaType = "audio/mp3";
        const int space = 5;

        var deliveryChannelPolicy = dlcsContext.DeliveryChannelPolicies.First(d => d.Customer == customerId &&
            d.Name == "default-audio");
        
        var dbEntry = dlcsContext.DefaultDeliveryChannels.Add(new DLCS.Model.DeliveryChannels.DefaultDeliveryChannel()
        {
            Customer = customerId,
            MediaType = mediaType,
            DeliveryChannelPolicyId = deliveryChannelPolicy.Id,
            Space = space
        });
        await dlcsContext.SaveChangesAsync();

        var path = $"customers/{customerId}/spaces/{space}/defaultDeliveryChannels/{dbEntry.Entity.Id}";

        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);
        var data = await response.ReadAsHydraResponseAsync<DefaultDeliveryChannel>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        data.MediaType.Should().Be(mediaType);
        data.Id.Should().Be($"{httpClient.BaseAddress}customers/{customerId}/spaces/{space}/defaultDeliveryChannels/{dbEntry.Entity.Id}");
    }
    
    [Fact]
    public async Task Post_CreatesDefaultDeliveryChannelsSpaceNotAvailableInCustomer_200()
    {
        // Arrange
        const string newCustomerJson = @"{
  ""@type"": ""Customer"",
  ""name"": ""api-test-customer-space"",
  ""displayName"": ""My New Customer""
}";
        var customerContent = new StringContent(newCustomerJson, Encoding.UTF8, "application/json");
        
        var customerResponse = await httpClient.AsAdmin().PostAsync("/customers", customerContent);
        var customerData = await customerResponse.ReadAsHydraResponseAsync<Customer>();
        var customerId = int.Parse(customerData.Id!.Split('/').Last());
        const int space = 5;
        var path = $"customers/{customerId}/spaces/{space}/defaultDeliveryChannels";

        string newDefaultDeliveryChannelJson = JsonConvert.SerializeObject(new DefaultDeliveryChannel()
        {
            MediaType = "image/tiff",
            Policy = "default",
            Channel = "iiif-img"
        });
        
        // Act
        var content = new StringContent(newDefaultDeliveryChannelJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        // Assert
        var data = await response.ReadAsHydraResponseAsync<DefaultDeliveryChannel>();

        var dbEntry =
            dlcsContext.DefaultDeliveryChannels.Include(d => d.DeliveryChannelPolicy)
                .Single(d => d.Customer == customerId &&
                             d.MediaType == "image/tiff" &&
                             d.DeliveryChannelPolicy.Channel == "iiif-img" &&
                             d.Space == space);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        data.MediaType.Should().Be("image/tiff");
        data.Id.Should().Be($"{httpClient.BaseAddress}customers/{customerId}/spaces/{space}/defaultDeliveryChannels/{dbEntry.Id}");
        dbEntry.DeliveryChannelPolicy.Name.Should().Be("default");

        var retrievalFromCustomer = await httpClient.AsCustomer(customerId)
            .GetAsync($"customers/spaces/{space}/defaultDeliveryChannels/{dbEntry.Id}");

        retrievalFromCustomer.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Theory]
    [InlineData("audio/mp3", "audio/*", "https://api.dlcs.io/customers/2/deliveryChannelPolicies/iiif-av/default-audio", "iiif-av")]
    [InlineData("video/mp4", "video/*", "default-video", "iiif-av")]
    [InlineData("image/tiff", "image/*", "default", "iiif-img")]
    [InlineData("image/*", "image/*", "use-original", "iiif-img")]
    public async Task Put_UpdatesDefaultDeliveryChannelForCustomerInSpace_200(string mediaType, string initialMediaType, string policyName, string channel)
    {
        // Arrange
        const string newCustomerJson = @"{
  ""@type"": ""Customer"",
  ""name"": ""api-test-customer-space-2"",
  ""displayName"": ""My New Customer""
}";
        var customerContent = new StringContent(newCustomerJson, Encoding.UTF8, "application/json");
        var customerResponse = await httpClient.AsAdmin().PostAsync("/customers", customerContent);
        var customerData = await customerResponse.ReadAsHydraResponseAsync<Customer>();
        var customerId = int.Parse(customerData.Id!.Split('/').Last());
        const int space = 5;

        var deliveryChannelPolicy = dlcsContext.DeliveryChannelPolicies.First(d => (d.Customer == customerId &&
            d.Name == policyName.Split("/", StringSplitOptions.None).Last()) || (d.Customer == 1 &&
            d.Name == policyName.Split("/", StringSplitOptions.None).Last()));
        
        var dbEntry = dlcsContext.DefaultDeliveryChannels.Add(new DLCS.Model.DeliveryChannels.DefaultDeliveryChannel()
        {
            Customer = customerId,
            MediaType = mediaType,
            DeliveryChannelPolicyId = deliveryChannelPolicy.Id,
            Space = space
        });

        await dlcsContext.SaveChangesAsync();
        
        var path = $"customers/{customerId}/spaces/{space}/defaultDeliveryChannels/{dbEntry.Entity.Id}";

        string newDefaultDeliveryChannelJson = JsonConvert.SerializeObject(new DefaultDeliveryChannel()
        {
            MediaType = mediaType,
            Policy = policyName,
            Channel = channel
        });
        
        // Act
        var content = new StringContent(newDefaultDeliveryChannelJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        // Assert
        var data = await response.ReadAsHydraResponseAsync<DefaultDeliveryChannel>();
        
        var modifiedDbEntry =
            dlcsContext.DefaultDeliveryChannels .Include(d => d.DeliveryChannelPolicy)
                .Single(d => d.Customer == customerId && 
                             d.MediaType == mediaType && d.DeliveryChannelPolicy.Channel == channel && d.Space == space);
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        data.MediaType.Should().Be(mediaType);
        data.Id.Should().Be($"{httpClient.BaseAddress}customers/{customerId}/spaces/{space}/defaultDeliveryChannels/{dbEntry.Entity.Id}");
        modifiedDbEntry.DeliveryChannelPolicy.Name.Should().Be(policyName.Split("/").Last());
    }
    
    [Fact]
    public async Task Delete_DeleteADefaultDeliveryChannelForCustomerAndSpace_200()
    {
        // Arrange
        const string newCustomerJson = @"{
  ""@type"": ""Customer"",
  ""name"": ""api-test-customer-space-3"",
  ""displayName"": ""My New Customer""
}";
        var customerContent = new StringContent(newCustomerJson, Encoding.UTF8, "application/json");
        var customerResponse = await httpClient.AsAdmin().PostAsync("/customers", customerContent);
        var customerData = await customerResponse.ReadAsHydraResponseAsync<Customer>();
        var customerId = int.Parse(customerData.Id!.Split('/').Last());
        
        var mediaType = "audio/mp3";
        const int space = 5;

        var deliveryChannelPolicy = dlcsContext.DeliveryChannelPolicies.First(d => d.Customer == customerId &&
            d.Name == "default-audio");
        
        var dbEntry = dlcsContext.DefaultDeliveryChannels.Add(new DLCS.Model.DeliveryChannels.DefaultDeliveryChannel()
        {
            Customer = customerId,
            MediaType = mediaType,
            DeliveryChannelPolicyId = deliveryChannelPolicy.Id,
            Space = space
        });
        await dlcsContext.SaveChangesAsync();
        
        var path = $"customers/{customerId}/spaces/{space}/defaultDeliveryChannels/{dbEntry.Entity.Id}";

        // Act
        var response = await httpClient.AsCustomer(customerId).DeleteAsync(path);

        var defaultDeliveryChannelAfterDelete = dlcsContext.DefaultDeliveryChannels.FirstOrDefault(d => 
            d.Customer == customerId && d.MediaType == mediaType && d.Space == space);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        defaultDeliveryChannelAfterDelete.Should().BeNull();
    }
}