using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;
using Xunit;

namespace Orchestrator.Tests.Integration
{
    /// <summary>
    /// Test of all auth handling
    /// </summary>
    [Trait("Category", "Integration")]
    [Collection(DatabaseCollection.CollectionName)]
    public class AuthHandlingTests : IClassFixture<ProtagonistAppFactory<Startup>>
    {
        private readonly DlcsDatabaseFixture dbFixture;
        private readonly HttpClient httpClient;

        public AuthHandlingTests(ProtagonistAppFactory<Startup> factory, DlcsDatabaseFixture databaseFixture)
        {
            dbFixture = databaseFixture;

            httpClient = factory
                .WithConnectionString(dbFixture.ConnectionString)
                .CreateClient();
            
            dbFixture.CleanUp();
        }
        
        [Fact]
        public async Task Get_Clickthrough_UnknownCustomer_Returns400()
        {
            // Arrange
            const string path = "auth/1/clickthrough";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Get_Clickthrough_UnknownRole_Returns404()
        {
            // Arrange
            const string path = "auth/99/passanger";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task Get_Clickthrough_CreatesDbRecordAndSetsCookie()
        {
            // Arrange
            await dbFixture.DbContext.SaveChangesAsync();
            
            var path = "auth/99/clickthrough";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.Should().ContainKey("Set-Cookie");
            var cookie = response.Headers.SingleOrDefault(header => header.Key == "Set-Cookie").Value.Single();
            cookie.Should().StartWith("dlcs-token-99");
            cookie.Should().Contain("samesite=none");
            cookie.Should().Contain("secure;");
            
            // E.g. dlcs-token-99=id%3D76e7d9fb-99ab-4b4f-87b0-f2e3f0e9664e; expires=Tue, 14 Sep 2021 16:53:53 GMT; domain=localhost; path=/; secure; samesite=none
            var toRemoveLength = "dlcs-token-99id%3D".Length;
            var cookieId = cookie.Substring(toRemoveLength + 1, cookie.IndexOf(';') - toRemoveLength - 1);
            
            var authToken = await dbFixture.DbContext.AuthTokens.SingleOrDefaultAsync(at => at.CookieId == cookieId);
            authToken.Should().NotBeNull();
            
            var sessionUser =
                await dbFixture.DbContext.SessionUsers.SingleOrDefaultAsync(su => su.Id == authToken.SessionUserId);
            sessionUser.Should().NotBeNull();
        }
    }
}