using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using API.Features.Customer.Requests;
using API.Infrastructure.Messaging;
using API.Tests.Integration.Infrastructure;
using DLCS.AWS.SNS;
using DLCS.HydraModel;
using DLCS.Repository;
using FakeItEasy;
using Hydra.Collections;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class CustomerTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsContext dbContext;
    private readonly HttpClient httpClient;
    private static readonly ICustomerNotificationSender NotificationSender = A.Fake<ICustomerNotificationSender>();

    public CustomerTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
    {
        dbContext = dbFixture.DbContext;
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithTestServices(services =>
            {
                services.AddSingleton(NotificationSender);
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
    public async Task GetCustomers_Returns_HydraCollection()
    {
        var response = await httpClient.AsCustomer().GetAsync("/customers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var coll = await response.ReadAsHydraResponseAsync<HydraCollection<JObject>>();
        coll.Should().NotBeNull();
        coll.Type.Should().Be("Collection");
        coll.Members.Should().NotBeEmpty();
        coll.Members.Should().Contain(jo => jo["@id"].Value<string>().EndsWith("customers/99"));
    }

    [Fact]
    public async Task GetCustomer_Returns_Customer()
    {
        var response = await httpClient.AsCustomer().GetAsync("/customers/99");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cust = await response.ReadAsHydraResponseAsync<Customer>();
        cust.Type.Should().Be("vocab:Customer");
        cust.Id.Should().EndWith("/customers/99");
    }

    [Fact]
    public async Task GetCustomer_Returns404_IfNotFound()
    {
        var response = await httpClient.AsAdmin().GetAsync("/customers/100");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateNewCustomer_CreatesCustomerAndAssociatedrecords()
    {
        var customerCounter = await dbContext.EntityCounters.SingleOrDefaultAsync(ec
            => ec.Customer == 0 && ec.Scope == "0" && ec.Type == "customer");
         customerCounter.Should().NotBeNull();

         const string newCustomerJson = @"{
  ""@type"": ""Customer"",
  ""name"": ""api-test-customer-1"",
  ""displayName"": ""My New Customer""
}";
        var content = new StringContent(newCustomerJson, Encoding.UTF8, "application/json");
        // act
        var response = await httpClient.AsAdmin().PostAsync("/customers", content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var newCustomer = await response.ReadAsHydraResponseAsync<Customer>();
        
        // The entity counter should allocate the next available ID.
        var newDbCustomer = dbContext.Customers.FirstOrDefault(c => c.Name == "api-test-customer-1")!;
        newDbCustomer.DisplayName.Should().Be(newCustomer.DisplayName);
        
        var newCustomerId = newDbCustomer.Id;
        newCustomer.Id.Should().EndWith("customers/" + newCustomerId);
        newDbCustomer.Should().NotBeNull();
        newDbCustomer.Name.Should().Be("api-test-customer-1");
        newDbCustomer.DisplayName.Should().Be("My New Customer");
        newDbCustomer.AcceptedAgreement.Should().BeTrue();
        newDbCustomer.Administrator.Should().BeFalse();

        // The global customer entity counter should be incremented
        customerCounter = await dbContext.EntityCounters.SingleAsync(ec
            => ec.Customer == 0 && ec.Scope == "0" && ec.Type == "customer");

        customerCounter.Should().NotBeNull("created on demand");
        customerCounter.Next.Should().Be(newCustomerId + 1);

        // There should be a space entity counter for the new customer
        var spaceCounter = await dbContext.EntityCounters.SingleOrDefaultAsync(ec
            => ec.Customer == newCustomerId && ec.Scope == newCustomerId.ToString() &&
               ec.Type == "space");
        spaceCounter.Should().NotBeNull();
        spaceCounter.Next.Should().Be(1);

        var customerAuthServices = await dbContext.AuthServices.Where(svc
            => svc.Customer == newCustomerId).ToListAsync();
        customerAuthServices.Should().HaveCount(2, "two new services created");

        var clickthroughService = customerAuthServices.SingleOrDefault(svc => svc.Name == "clickthrough");
        var logoutService = customerAuthServices.SingleOrDefault(svc => svc.Name == "logout");
        clickthroughService.Should().NotBeNull();
        logoutService.Should().NotBeNull();
        clickthroughService.Id.Should().NotBeEmpty();
        logoutService.Id.Should().NotBeEmpty();
        clickthroughService.ChildAuthService.Should().Be(logoutService.Id);
        logoutService.Profile.Should().Be("http://iiif.io/api/auth/1/logout");
        clickthroughService.Ttl.Should().Be(600);
        logoutService.Ttl.Should().Be(600);

        // Role: Should be a clickthrough Role for AuthService
        var roles = await dbContext.Roles.Where(role
            => role.Customer == newCustomerId).ToListAsync();
        roles.Should().HaveCount(1); // one new role
        var clickthroughRole = roles.SingleOrDefault(role => role.Name == "clickthrough");
        clickthroughRole.Should().NotBeNull();
        clickthroughRole.AuthService.Should().Be(clickthroughService.Id);

        // What should this URL be? api.dlcs.io is... not right?
        var roleId = $"https://api.dlcs.io/customers/{newCustomerId}/roles/clickthrough";
        clickthroughRole.Id.Should().Be(roleId);

        // Should be a row in Queues
        var defaultQueue =
            await dbContext.Queues.SingleAsync(q => q.Customer == newCustomerId && q.Name == "default");
        defaultQueue.Size.Should().Be(0);
        
        var priorityQueue =
            await dbContext.Queues.SingleAsync(q => q.Customer == newCustomerId && q.Name == "priority");
        priorityQueue.Size.Should().Be(0);
        
        dbContext.DeliveryChannelPolicies.Count(d => d.Customer == newDbCustomer.Id).Should().Be(3);
        dbContext.DefaultDeliveryChannels.Count(d => d.Customer == newDbCustomer.Id).Should().Be(5);

        A.CallTo(() =>
                NotificationSender.SendCustomerCreatedMessage(
                    A<DLCS.Model.Customers.Customer>.That.Matches(c => c.Id == newDbCustomer.Id),
                    A<CancellationToken>._))
            .MustHaveHappened();
    }

    [Fact]
    public async Task CreateNewCustomer_HandlesRaceConditionOnName()
    {
        var customerName = nameof(CreateNewCustomer_HandlesRaceConditionOnName);

        var customer1 = CreateCustomer(customerName, "1");
        var customer2 = CreateCustomer(customerName, "2");
        await Task.WhenAll(customer1, customer2);

        // Assert that we get a 201 and a 409. One should succeed and the other fail. Doesn't matter which
        var customer1Status = customer1.Result.StatusCode;
        var customer2Status = customer2.Result.StatusCode;

        if (customer1Status == HttpStatusCode.Created)
        {
            customer2Status.Should().Be(HttpStatusCode.Conflict, "Req1 passed so 2 should fail");
        }
        else if (customer2Status == HttpStatusCode.Created)
        {
            customer1Status.Should().Be(HttpStatusCode.Conflict, "Req2 passed so 1 should fail");
        }
        else
        {
            throw new ApplicationException("Something went wrong - race condition should result in 1 of the 2 failing");
        }

        dbContext.Customers.Count(c => c.Name == customerName)
            .Should().Be(1, "Race condition so one request should succeed");
    }
    
    [Fact]
    public async Task CreateNewCustomer_HandlesRaceConditionOnDisplayName()
    {
        var displayName = nameof(CreateNewCustomer_HandlesRaceConditionOnDisplayName);

        var customer1 = CreateCustomer("customername_1", displayName);
        var customer2 = CreateCustomer("customername_2", displayName);
        await Task.WhenAll(customer1, customer2);

        // Assert that we get a 201 and a 409. One should succeed and the other fail. Doesn't matter which
        var customer1Status = customer1.Result.StatusCode;
        var customer2Status = customer2.Result.StatusCode;

        if (customer1Status == HttpStatusCode.Created)
        {
            customer2Status.Should().Be(HttpStatusCode.Conflict, "Req1 passed so 2 should fail");
        }
        else if (customer2Status == HttpStatusCode.Created)
        {
            customer1Status.Should().Be(HttpStatusCode.Conflict, "Req2 passed so 1 should fail");
        }
        else
        {
            throw new ApplicationException("Something went wrong - race condition should result in 1 of the 2 failing");
        }

        dbContext.Customers.Count(c => c.DisplayName == displayName)
            .Should().Be(1, "Race condition so one request should succeed");
    }

    private Task<HttpResponseMessage> CreateCustomer(string name, string displayName)
    {
        var customer1Json = $@"{{
  ""@type"": ""Customer"",
  ""name"": ""{name}"",
  ""displayName"": ""{displayName}""
}}";
        var content = new StringContent(customer1Json, Encoding.UTF8, "application/json");
        return httpClient.AsAdmin().PostAsync("/customers", content);
    }

    [Fact]
    public async Task CreateNewCustomer_Returns409_IfNameConflicts()
    {
        // These display names have already been taken by the seed data
        const string newCustomerJson = @"{
  ""@type"": ""Customer"",
  ""name"": ""test"",
  ""displayName"": ""TestUser""
}";

        // act
        var content = new StringContent(newCustomerJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsAdmin().PostAsync("/customers", content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
    
    [Fact]
    public async Task CreateNewCustomer_RollsBackSuccessfully_WhenDeliveryChannelsNotCreatedSuccessfully()
    {
        var expectedCustomerId = (int)dbContext.EntityCounters.Single(c => c.Type == "customer" && c.Scope == "0" && c.Customer == 0).Next;

        const string url = "/customers";
        const string customerJson = @"{
  ""name"": ""customerApiTest2"",
  ""displayName"": ""testing api customer 2""
    }";
        var content = new StringContent(customerJson, Encoding.UTF8, "application/json");

        dbContext.DeliveryChannelPolicies.Add(new DLCS.Model.Policies.DeliveryChannelPolicy()
        {
            Id = 250,
            DisplayName = "A default audio policy",
            Name = "default-audio",
            Customer = expectedCustomerId,
            Channel = "iiif-av",
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            PolicyData = null,
            System = false
        }); // creates a duplicate policy, causing an error

        await dbContext.SaveChangesAsync();


        // Act
        var response = await httpClient.AsAdmin(1).PostAsync(url, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        dbContext.DeliveryChannelPolicies.Count(d => d.Customer == expectedCustomerId).Should()
            .Be(1, "difference of 1 due to delivery channel added above");
        dbContext.DefaultDeliveryChannels.Count(d => d.Customer == expectedCustomerId).Should().Be(0);
        dbContext.Customers.FirstOrDefault(c => c.Id == expectedCustomerId).Should().BeNull();
        dbContext.EntityCounters.Count(e => e.Customer == expectedCustomerId).Should().Be(0);
        dbContext.Roles.Count(r => r.Customer == expectedCustomerId).Should().Be(0);
    }

    [Fact] 
    public async Task CreateNewCustomer_Returns400_IfNameStartsWithVersion()
    {
        // Tests HydraCustomerValidator:StartsWithVersion
        const string newCustomerJson = @"{
  ""@type"": ""Customer"",
  ""name"": ""v2"",
  ""displayName"": ""TestUser""
}";
        
        // act
        var content = new StringContent(newCustomerJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsAdmin().PostAsync("/customers", content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact] 
    public async Task CreateNewCustomer_Returns400_IfNameReserved()
    {
        // Tests HydraCustomerValidator:IsReservedWord
        const string newCustomerJson = @"{
  ""@type"": ""Customer"",
  ""name"": ""Admin"",
  ""displayName"": ""TestUser""
}";
        
        // act
        var content = new StringContent(newCustomerJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsAdmin().PostAsync("/customers", content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Non_Admin_Cant_Create_Customer()
    {
        const string newCustomerJson = @"{
  ""@type"": ""Customer"",
  ""name"": ""test"",
  ""displayName"": ""TestUser""
}";

        // act
        var content = new StringContent(newCustomerJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PostAsync("/customers", content);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Patch_Returns400_IfValidationFails()
    {
        // This doesn't validate all possible invalid requests, see Validator tests
        // Arrange
        const string patchJson = @"{
  ""@type"": ""Customer"",
  ""Name"": ""A new name""
}";
        
        var content = new StringContent(patchJson, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer().PatchAsync("/customers/2", content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Patch_Returns404_IfCustomerNotFound()
    {
        // Arrange
        const string patchJson = @"{
  ""@type"": ""Customer"",
  ""DisplayName"": ""A new name""
}";
        
        var content = new StringContent(patchJson, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsAdmin().PatchAsync("/customers/-2", content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Patch_Returns200_IfCustomerUpdateSuccessful()
    {
        // Arrange
        const string customerName = nameof(Patch_Returns200_IfCustomerUpdateSuccessful);
        await dbContext.Customers.AddTestCustomer(10, customerName, customerName);
        await dbContext.SaveChangesAsync();
        
        const string patchJson = @"{
  ""@type"": ""Customer"",
  ""DisplayName"": ""A new name""
}";
        
        var content = new StringContent(patchJson, Encoding.UTF8, "application/json");
        
        // Act
        var response = await httpClient.AsCustomer().PatchAsync("/customers/10", content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var newCustomer = await response.ReadAsHydraResponseAsync<Customer>();
        newCustomer.DisplayName.Should().Be("A new name");
    }
}