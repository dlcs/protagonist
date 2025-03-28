using System;
using System.Net;
using System.Net.Http;
using System.Text;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.Core.Strings;
using DLCS.HydraModel;
using DLCS.Repository;
using Hydra;
using Hydra.Collections;
using Microsoft.EntityFrameworkCore;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class UsersAndKeysTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsContext dbContext;
    private readonly HttpClient httpClient;

    public UsersAndKeysTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
    {
        dbContext = dbFixture.DbContext;
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(dbFixture, "API-Test");
        dbFixture.CleanUp();
    }
    
    
    [Fact]
    public async Task Api_Grants_Key_And_Secret()
    {
        var response = await httpClient.AsCustomer(99).PostAsync("/customers/99/keys", new StringContent(String.Empty));
        var key = await response.ReadAsHydraResponseAsync<ApiKey>();
        key.Key.Should().NotBeEmpty();
        key.Secret.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Api_Yields_Keys()
    {
        // arrange
        var response1 = await httpClient.AsCustomer(99).PostAsync("/customers/99/keys", new StringContent(String.Empty));
        var key1 = await response1.ReadAsHydraResponseAsync<ApiKey>();
        var response2 = await httpClient.AsCustomer(99).PostAsync("/customers/99/keys", new StringContent(String.Empty));
        var key2 = await response2.ReadAsHydraResponseAsync<ApiKey>();
        
        // act
        var response = await httpClient.AsCustomer(99).GetAsync("/customers/99/keys");
        var keys = await response.ReadAsHydraResponseAsync<HydraCollection<ApiKey>>();
        
        // assert
        keys.Members.Should().Contain(k => k.Key == key1.Key);
        keys.Members.Should().Contain(k => k.Key == key2.Key);
        keys.Members.Should().NotContain(k => k.Secret.HasText());
    }

    [Fact]
    public async Task Api_Key_Can_Be_Deleted()
    {
        // arrange
        var response1 = await httpClient.AsCustomer(99).PostAsync("/customers/99/keys", new StringContent(String.Empty));
        var key1 = await response1.ReadAsHydraResponseAsync<ApiKey>();

        var dbCust = await dbContext.Customers.AsNoTracking().SingleAsync(c => c.Id == 99);
        var startKeys = dbCust.Keys;
        
        // act 
        var response = await httpClient.AsCustomer(99).DeleteAsync($"/customers/99/keys/{key1.Key}");
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        dbCust = await dbContext.Customers.FindAsync(99);
        var endKeys = dbCust.Keys;
        startKeys.Should().Contain(key1.Key);
        endKeys.Should().NotContain(key1.Key);
    }

    
    [Fact]
    public async Task Admin_User_Cant_Delete_Last_Key()
    {
        // arrange
        var key1 = Guid.NewGuid().ToString();
        var key2 = Guid.NewGuid().ToString();
        var admin = new DLCS.Model.Customers.Customer()
        {
            Administrator = true,
            Created = DateTime.UtcNow,
            Keys = new[] { key1, key2 },
            Name = "admin",
            AcceptedAgreement = true,
            DisplayName = "Admin customer",
            Id = 2 
        };
        await dbContext.Customers.AddAsync(admin);
        await dbContext.SaveChangesAsync();
        
        // act
        var response1 = await httpClient.AsAdmin().DeleteAsync($"/customers/2/keys/{key1}");
        response1.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var response2 = await httpClient.AsAdmin().DeleteAsync($"/customers/2/keys/{key2}");
        
        // assert
        response2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Customer_Can_Create_PortalUser()
    {        
        // arrange
        const string portalUserJson = @"{
  ""@type"": ""User"",
  ""email"": ""user@email.com"",
  ""password"": ""password123""
}";
        
        // act
        var content = new StringContent(portalUserJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PostAsync("/customers/99/portalUsers", content);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var portalUser = await response.ReadAsHydraResponseAsync<PortalUser>();
        portalUser.Id.Should().Contain("/customers/99/portalUsers/");
        portalUser.Email.Should().Be("user@email.com");
        portalUser.Password.Should().BeNullOrEmpty(); // don't return password!

    }

    [Fact]
    public async Task PortalUsers_Returned_For_Customer()
    {
        // arrange
        await dbContext.Users.AddTestUser(99, "user1@cust99.org");
        await dbContext.Users.AddTestUser(99, "user2@cust99.org");
        await dbContext.Users.AddTestUser(99, "user3@cust99.org");
        await dbContext.SaveChangesAsync();
        
        // act
        var response = await httpClient.AsCustomer(99).GetAsync("/customers/99/portalUsers");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.ReadAsHydraResponseAsync<HydraCollection<PortalUser>>();
        users.Type.Should().Be("Collection");
        users.Members.Should().HaveCountGreaterThan(2);
        users.Members.Should().Contain(pu => pu.Email == "user1@cust99.org");
        users.Members.Should().Contain(pu => pu.Email == "user3@cust99.org");
        users.Members.Should().NotContain(pu => pu.Password.HasText());
    }
    
    

    [Fact]
    public async Task Get_PortalUser_By_Id()
    {
        // arrange
        var dbUser = await dbContext.Users.AddTestUser(99, "user100@cust99.org");
        var userId = dbUser.Entity.Id;
        await dbContext.SaveChangesAsync();
        
        // act
        var response = await httpClient.AsCustomer(99).GetAsync($"/customers/99/portalUsers/{userId}");
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.ReadAsHydraResponseAsync<PortalUser>();
        user.Type.Should().Be("vocab:PortalUser");
        user.Id.Should().EndWith($"/customers/99/portalUsers/{userId}");
        user.Email.Should().Be("user100@cust99.org");
        user.Password.Should().BeNullOrEmpty();
    }
    
    
    [Fact]
    public async Task Get_PortalUser_By_Id_Returns_404_If_No_User()
    {
        // act
        var response = await httpClient.AsCustomer(99).GetAsync($"/customers/99/portalUsers/not-a-real-id");
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task User_Can_Change_Email()
    {
        // arrange
        var dbUser = await dbContext.Users.AddTestUser(99, "user101@cust99.org");
        var userId = dbUser.Entity.Id;
        await dbContext.SaveChangesAsync();
        
        const string portalUserJson = @"{
  ""@type"": ""User"",
  ""email"": ""user102@new-email.com""
}";
        var content = new StringContent(portalUserJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync($"/customers/99/portalUsers/{userId}", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.ReadAsHydraResponseAsync<PortalUser>();
        user.Type.Should().Be("vocab:PortalUser");
        user.Id.Should().EndWith($"/customers/99/portalUsers/{userId}");
        user.Email.Should().Be("user102@new-email.com");
        user.Password.Should().BeNullOrEmpty();
    }
    
    [Fact]
    public async Task User_Can_Change_Password()
    {
        // arrange
        string portalUserJson = @"{
  ""@type"": ""User"",
  ""email"": ""user201@email.com"",
  ""password"": ""password-1""
}";
        var content = new StringContent(portalUserJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PostAsync("/customers/99/portalUsers", content);
        var portalUser = await response.ReadAsHydraResponseAsync<PortalUser>();
        var dbId = portalUser.Id.GetLastPathElement();
        var dbUser = await dbContext.Users.FindAsync(dbId);
        var startPasswordEnc = dbUser.EncryptedPassword;
        
        // act
        portalUserJson = @"{
  ""@type"": ""User"",
  ""password"": ""password-2""
}";
        content = new StringContent(portalUserJson, Encoding.UTF8, "application/json");
        response = await httpClient.AsCustomer(99).PatchAsync($"/customers/99/portalUsers/{dbId}", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // assert
        var user = await response.ReadAsHydraResponseAsync<PortalUser>();
        user.Password.Should().BeNullOrEmpty();
        dbUser = await dbContext.Users.FindAsync(dbId);
        dbUser.EncryptedPassword.Should().NotBeEmpty();
        dbUser.EncryptedPassword.Should().NotBe(startPasswordEnc);
    }

    [Fact]
    public async Task User_Can_Be_Deleted()
    {
        // arrange
        var dbUser = await dbContext.Users.AddTestUser(99, "user401@cust99.org");
        var userId = dbUser.Entity.Id;
        await dbContext.SaveChangesAsync();
        
        // act
        var response = await httpClient.AsCustomer(99).DeleteAsync($"/customers/99/portalUsers/{userId}");
        
        // assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var deletedUser = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId);
        deletedUser.Should().BeNull();
    }

}
