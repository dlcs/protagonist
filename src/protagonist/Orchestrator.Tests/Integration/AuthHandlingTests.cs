using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using DLCS.Core.Types;
using IIIF.Auth.V2;
using IIIF.Serialisation;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Orchestrator.Tests.Integration.Infrastructure;
using Stubbery;
using Test.Helpers;
using Test.Helpers.Integration;

namespace Orchestrator.Tests.Integration;

/// <summary>
/// Test of all auth handling
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class AuthHandlingTests : IClassFixture<ProtagonistAppFactory<Startup>>, IClassFixture<ApiStub>
{
    private readonly DlcsDatabaseFixture dbFixture;
    private readonly HttpClient httpClient;
    private readonly ApiStub apiStub;

    public AuthHandlingTests(ProtagonistAppFactory<Startup> factory, ApiStub apiStub, DlcsDatabaseFixture databaseFixture)
    {
        apiStub.SafeStart();
        this.apiStub = apiStub; 
        
        dbFixture = databaseFixture;

        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithConfigValue("Auth:Auth2ServiceRoot", apiStub.Address)
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
    public async Task Get_UnknownRole_Returns404()
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
        var path = "auth/99/clickthrough";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("Set-Cookie");
        var cookie = response.Headers.SingleOrDefault(header => header.Key == "Set-Cookie").Value.First();
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

    #region Token - Non-Browser Clients
    [Fact]
    public async Task Get_Token_Returns401_WithErrorJson_IfNoCookie_AndMessageIdNotPresent()
    {
        // Arrange
        const string path = "/auth/99/token";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var responseBody = JObject.Parse(await response.Content.ReadAsStringAsync());
        responseBody["error"].Value<string>().Should().Be("missingCredentials");
        responseBody["description"].Value<string>().Should().Be("Required cookie missing");
    }
    
    [Fact]
    public async Task Get_Token_Returns403_WithErrorJson_IfCookieDoesNotContainId_AndMessageIdNotPresent()
    {
        // Arrange
        const string path = "/auth/99/token";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", "dlcs-token-99=unexpected-value;");
        
        // Act
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var responseBody = JObject.Parse(await response.Content.ReadAsStringAsync());
        responseBody["error"].Value<string>().Should().Be("invalidCredentials");
        responseBody["description"].Value<string>().Should().Be("Id not found in cookie");
    }
    
    [Fact]
    public async Task Get_Token_Returns403_WithErrorJson_IfCookieDoesNotContainKnownId_AndMessageIdNotPresent()
    {
        // Arrange
        const string path = "/auth/99/token";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-token-99=id={Guid.NewGuid().ToString()};");
        
        // Act
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var responseBody = JObject.Parse(await response.Content.ReadAsStringAsync());
        responseBody["error"].Value<string>().Should().Be("invalidCredentials");
        responseBody["description"].Value<string>().Should().Be("Credentials provided unknown or expired");
    }
    
    [Fact]
    public async Task Get_Token_Returns403_WithErrorJson_IfCookieContainsId_ForDifferentCustomer_AndMessageIdNotPresent()
    {
        // Arrange
        var token = await dbFixture.DbContext.AuthTokens.AddTestToken(customer: -1);
        await dbFixture.DbContext.SaveChangesAsync();
        const string path = "/auth/99/token";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-token-99=id={token.Entity.CookieId};");
        
        // Act
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var responseBody = JObject.Parse(await response.Content.ReadAsStringAsync());
        responseBody["error"].Value<string>().Should().Be("invalidCredentials");
        responseBody["description"].Value<string>().Should().Be("Credentials provided unknown or expired");
    }
    
    [Fact]
    public async Task Get_Token_Returns403_WithErrorJson_IfCookieContainsExpiredId_AndMessageIdNotPresent()
    {
        // Arrange
        var token = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddHours(-1));
        await dbFixture.DbContext.SaveChangesAsync();
        const string path = "/auth/99/token";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-token-99=id={token.Entity.CookieId};");
        
        // Act
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var responseBody = JObject.Parse(await response.Content.ReadAsStringAsync());
        responseBody["error"].Value<string>().Should().Be("invalidCredentials");
        responseBody["description"].Value<string>().Should().Be("Credentials provided unknown or expired");
    }
    
    [Fact]
    public async Task Get_Token_Returns200_WithAccessToken_IfSuccess_AndMessageIdNotPresent()
    {
        // Arrange
        var token = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(10));
        await dbFixture.DbContext.SaveChangesAsync();
        const string path = "/auth/99/token";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-token-99=id={token.Entity.CookieId};");
        
        // Act
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseBody = JObject.Parse(await response.Content.ReadAsStringAsync());
        responseBody["accessToken"].Value<string>().Should().Be(token.Entity.BearerToken);
        responseBody["expiresIn"].Value<int>().Should().Be(token.Entity.Ttl);
    }
    #endregion
    
    #region Token - Browser-Based Clients
    [Fact]
    public async Task Get_Token_ReturnsView_WithErrorJson_IfNoCookie()
    {
        // Arrange
        const string path = "/auth/99/token?messageId=123";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseBody = await ParseHtmlTokenReponse(response);
        responseBody["error"].Value<string>().Should().Be("missingCredentials");
        responseBody["description"].Value<string>().Should().Be("Required cookie missing");
    }

    [Fact]
    public async Task Get_Token_ReturnsView_WithErrorJson_IfCookieDoesNotContainId()
    {
        // Arrange
        const string path = "/auth/99/token?messageId=123";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", "dlcs-token-99=unexpected-value;");
        
        // Act
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseBody = await ParseHtmlTokenReponse(response);
        responseBody["error"].Value<string>().Should().Be("invalidCredentials");
        responseBody["description"].Value<string>().Should().Be("Id not found in cookie");
    }
    
    [Fact]
    public async Task Get_Token_ReturnsView_WithErrorJson_IfCookieDoesNotContainKnownId()
    {
        // Arrange
        const string path = "/auth/99/token?messageId=123";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-token-99=id={Guid.NewGuid().ToString()};");
        
        // Act
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseBody = await ParseHtmlTokenReponse(response);
        responseBody["error"].Value<string>().Should().Be("invalidCredentials");
        responseBody["description"].Value<string>().Should().Be("Credentials provided unknown or expired");
    }
    
    [Fact]
    public async Task Get_Token_ReturnsView_WithErrorJson_IfCookieContainsId_ForDifferentCustomer()
    {
        // Arrange
        var token = await dbFixture.DbContext.AuthTokens.AddTestToken(customer: -1);
        await dbFixture.DbContext.SaveChangesAsync();
        const string path = "/auth/99/token?messageId=123";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-token-99=id={token.Entity.CookieId};");
        
        // Act
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseBody = await ParseHtmlTokenReponse(response);
        responseBody["error"].Value<string>().Should().Be("invalidCredentials");
        responseBody["description"].Value<string>().Should().Be("Credentials provided unknown or expired");
    }
    
    [Fact]
    public async Task Get_Token_ReturnsView_WithErrorJson_IfCookieContainsExpiredId()
    {
        // Arrange
        var token = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddHours(-1));
        await dbFixture.DbContext.SaveChangesAsync();
        const string path = "/auth/99/token?messageId=123";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-token-99=id={token.Entity.CookieId};");
        
        // Act
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseBody = await ParseHtmlTokenReponse(response);
        responseBody["error"].Value<string>().Should().Be("invalidCredentials");
        responseBody["description"].Value<string>().Should().Be("Credentials provided unknown or expired");
    }
    
    [Fact]
    public async Task Get_Token_ReturnsView_WithAccessToken_IfSuccess()
    {
        // Arrange
        var token = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(10));
        await dbFixture.DbContext.SaveChangesAsync();
        const string path = "/auth/99/token?messageId=123";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-token-99=id={token.Entity.CookieId};");
        
        // Act
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseBody = await ParseHtmlTokenReponse(response);
        responseBody["accessToken"].Value<string>().Should().Be(token.Entity.BearerToken);
        responseBody["expiresIn"].Value<int>().Should().Be(token.Entity.Ttl);
        responseBody["messageId"].Value<string>().Should().Be("123");
    }
    #endregion

    [Fact]
    public async Task ProbeService_ReturnsProbeResultWith401Status_IfNoAccessToken()
    {
        // Arrange
        var path = "auth/v2/probe/99/1/asset";
        
        // Act
        var result = await httpClient.GetAsync(path);
        
        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Headers.CacheControl.Private.Should().BeTrue();

        var probeResult2 = (await result.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        probeResult2.Status.Should().Be(401);
    }
    
    [Fact]
    public async Task ProbeService_404_IfAssetNotFound()
    {
        // Arrange
        var path = "auth/v2/probe/99/1/not-found-asset";
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("bearer", "12345");
        var result = await httpClient.SendAsync(request);
        
        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result.Content.Headers.ContentType.MediaType
            .Should().Be("application/problem+json", "this isn't an AuthProbeResult2");
    }

    [Fact]
    public async Task ProbeService_ReturnsProbeResultWith200Status_IfOpen()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(ProbeService_ReturnsProbeResultWith200Status_IfOpen)}");
        await dbFixture.DbContext.Images.AddTestAsset(id);
        await dbFixture.DbContext.SaveChangesAsync();
        var path = $"auth/v2/probe/{id}";
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("bearer", "12345");
        var result = await httpClient.SendAsync(request);
        
        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Headers.CacheControl.Private.Should().BeTrue("asset is open but all auth responses should be private");

        var probeResult2 = (await result.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        probeResult2.Status.Should().Be(200);
    }
    
    [Fact]
    public async Task ProbeService_ReturnsProbeResultWith200Status_IfHasMaxUnauth_WithoutRoles()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(ProbeService_ReturnsProbeResultWith200Status_IfHasMaxUnauth_WithoutRoles)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, maxUnauthorised: 100);
        await dbFixture.DbContext.SaveChangesAsync();
        var path = $"auth/v2/probe/{id}";
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("bearer", "12345");
        var result = await httpClient.SendAsync(request);
        
        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Headers.CacheControl.Private.Should().BeTrue();

        var probeResult2 = (await result.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        probeResult2.Status.Should().Be(200);
    }

    [Fact]
    public async Task ProbeService_ReturnsProbeResult_FromDownstreamAuthService()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(ProbeService_ReturnsProbeResult_FromDownstreamAuthService)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, maxUnauthorised: 100, roles: "test-role");
        await dbFixture.DbContext.SaveChangesAsync();

        var downstreamProbeResult = new AuthProbeResult2 { Status = 999 };
        apiStub
            .Get($"probe_internal/{id}?roles=test-role", (_, _) => downstreamProbeResult.AsJson())
            .IfHeader("Authorization", "Bearer 12345");
        
        var path = $"auth/v2/probe/{id}";
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("bearer", "12345");
        var result = await httpClient.SendAsync(request);
        
        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Headers.CacheControl.Private.Should().BeTrue();

        var probeResult2 = (await result.Content.ReadAsStreamAsync()).FromJsonStream<AuthProbeResult2>();
        probeResult2.Should().BeEquivalentTo(downstreamProbeResult);
    }
    
    private async Task<JObject> ParseHtmlTokenReponse(HttpResponseMessage response)
    {
        var htmlParser = new HtmlParser();
        var regex = new Regex("window.parent.postMessage\\(({.*}),.*");
        
        var document = htmlParser.ParseDocument(await response.Content.ReadAsStreamAsync());
        var scriptElement = document.QuerySelector("script") as IHtmlScriptElement;
        
        var text = scriptElement.Text
            .Replace("\n", string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\t", string.Empty);
        var jsonString = regex.Match(text);
        return JObject.Parse(jsonString.Groups[1].ToString());
    }
}