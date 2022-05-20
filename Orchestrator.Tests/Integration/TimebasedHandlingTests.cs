﻿using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;
using Xunit;
using Yarp.ReverseProxy.Forwarder;

namespace Orchestrator.Tests.Integration
{
    /// <summary>
    /// Test of all requests handled by custom iiif-img handling
    /// </summary>
    [Trait("Category", "Integration")]
    [Collection(DatabaseCollection.CollectionName)]
    public class TimebasedHandlingTests : IClassFixture<ProtagonistAppFactory<Startup>>
    {
        private readonly DlcsDatabaseFixture dbFixture;
        private readonly HttpClient httpClient;

        public TimebasedHandlingTests(ProtagonistAppFactory<Startup> factory, DlcsDatabaseFixture dbFixture)
        {
            this.dbFixture = dbFixture;
            httpClient = factory
                .WithConnectionString(dbFixture.ConnectionString)
                .WithTestServices(services =>
                {
                    services
                        .AddSingleton<IForwarderHttpClientFactory, TestProxyHttpClientFactory>()
                        .AddSingleton<IHttpForwarder, TestProxyForwarder>()
                        .AddSingleton<TestProxyHandler>();
                })
                .CreateClient(new WebApplicationFactoryClientOptions {AllowAutoRedirect = false});
            
            dbFixture.CleanUp();
        }
        
        [Fact]
        public async Task Get_UnknownCustomer_Returns404()
        {
            // Arrange
            const string path = "iiif-av/1/1/my-timebased/full/full/max/max/0/default.mp4";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task Get_UnknownSpace_Returns404()
        {
            // Arrange
            const string path = "iiif-av/99/5/my-timebased/full/full/max/max/0/default.mp4";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task Get_UnknownImage_Returns404()
        {
            // Arrange
            const string path = "iiif-av/99/1/my-timebased/full/full/max/max/0/default.mp4";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Get_AssetDoesNotRequireAuth_Returns302ToS3Location()
        {
            // Arrange
            var id = "99/1/test-noauth";
            await dbFixture.DbContext.Images.AddTestAsset(id, family: AssetFamily.Timebased, mediaType: "video/mpeg",
                maxUnauthorised: -1, origin: "/test/space");
            await dbFixture.DbContext.SaveChangesAsync();
            var expectedPath =
                new Uri(
                    "https://s3-eu-west-1.amazonaws.com/test-dlcs-storage/99/1/test-noauth/full/full/max/max/0/default.mp4");
            
            // Act
            var response = await httpClient.GetAsync("/iiif-av/99/1/test-noauth/full/full/max/max/0/default.mp4");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location.Should().Be(expectedPath);
        }
        
        [Fact]
        public async Task Get_AssetRequiresAuth_Returns401_IfNoAuthProvided()
        {
            // Arrange
            string id = "99/1/test-auth";
            await dbFixture.DbContext.Images.AddTestAsset(id, family: AssetFamily.Timebased, mediaType: "video/mpeg",
                maxUnauthorised: 100, origin: "/test/space", roles: "basic");
            await dbFixture.DbContext.SaveChangesAsync();

            // Act
            var response = await httpClient.GetAsync("/iiif-av/99/1/test-auth/full/full/max/max/0/default.mp4");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        
        [Fact]
        public async Task Get_AssetRequiresAuth_Returns401_IfBearerTokenProvided_ButInvalid()
        {
            // Arrange
            var id = "99/1/bearer-fail";
            await dbFixture.DbContext.Images.AddTestAsset(id, family: AssetFamily.Timebased, mediaType: "video/mpeg",
                maxUnauthorised: 100, origin: "/test/space", roles: "basic");
            await dbFixture.DbContext.SaveChangesAsync();
            const string bearerToken = "ababababab";

            // Act
            var request = new HttpRequestMessage(HttpMethod.Get,
                "/iiif-av/99/1/bearer-fail/full/full/max/max/0/default.mp4");
            request.Headers.Add("Authorization", $"bearer {bearerToken}");
            var response = await httpClient.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        
        [Fact]
        public async Task Get_AssetRequiresAuth_Returns401_IfBearerTokenValid()
        {
            // Arrange
            var id = "99/1/bearer-pass";
            await dbFixture.DbContext.Images.AddTestAsset(id, family: AssetFamily.Timebased, mediaType: "video/mpeg",
                maxUnauthorised: 100, origin: "/test/space", roles: "clickthrough");
            var userSession =
                await dbFixture.DbContext.SessionUsers.AddTestSession(
                    DlcsDatabaseFixture.ClickThroughAuthService.AsList());
            var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(15),
                sessionUserId: userSession.Entity.Id);
            await dbFixture.DbContext.SaveChangesAsync();
            
            // Act
            var request = new HttpRequestMessage(HttpMethod.Get,
                "/iiif-av/99/1/bearer-pass/full/full/max/max/0/default.mp4");
            request.Headers.Add("Authorization", $"bearer {authToken.Entity.BearerToken}");
            var response = await httpClient.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        
        [Fact]
        public async Task Head_AssetRequiresAuth_Returns200_IfBearerTokenValid()
        {
            // Arrange
            var id = "99/1/bearer-head";
            await dbFixture.DbContext.Images.AddTestAsset(id, family: AssetFamily.Timebased, mediaType: "video/mpeg",
                maxUnauthorised: 100, origin: "/test/space", roles: "clickthrough");
            var userSession =
                await dbFixture.DbContext.SessionUsers.AddTestSession(
                    DlcsDatabaseFixture.ClickThroughAuthService.AsList());
            var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(15),
                sessionUserId: userSession.Entity.Id);
            await dbFixture.DbContext.SaveChangesAsync();

            // Act
            var request = new HttpRequestMessage(HttpMethod.Head,
                "/iiif-av/99/1/bearer-head/full/full/max/max/0/default.mp4");
            request.Headers.Add("Authorization", $"bearer {authToken.Entity.BearerToken}");
            var response = await httpClient.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        
        [Fact]
        public async Task Head_AssetRequiresAuth_Returns401_IfBearerTokenProvided_ButInvalid()
        {
            // Arrange
            var id = "99/1/bearer-head-invalid";
            await dbFixture.DbContext.Images.AddTestAsset(id, family: AssetFamily.Timebased, mediaType: "video/mpeg",
                maxUnauthorised: 100, origin: "/test/space", roles: "clickthrough");
            var userSession =
                await dbFixture.DbContext.SessionUsers.AddTestSession(
                    DlcsDatabaseFixture.ClickThroughAuthService.AsList());
            var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(-15),
                sessionUserId: userSession.Entity.Id);
            await dbFixture.DbContext.SaveChangesAsync();

            // Act
            var request = new HttpRequestMessage(HttpMethod.Head, $"/iiif-av/{id}/full/full/max/max/0/default.mp4");
            request.Headers.Add("Authorization", $"bearer {authToken.Entity.BearerToken}");
            var response = await httpClient.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        
        [Fact]
        public async Task Get_AssetRequiresAuth_Returns401_IfCookieProvided_ButInvalid()
        {
            // Arrange
            var id = "99/1/cookie-fail";
            await dbFixture.DbContext.Images.AddTestAsset(id, family: AssetFamily.Timebased, mediaType: "video/mpeg",
                maxUnauthorised: 100, origin: "/test/space", roles: "basic");
            await dbFixture.DbContext.SaveChangesAsync();

            // Act
            var request = new HttpRequestMessage(HttpMethod.Get,
                "/iiif-av/99/1/cookie-fail/full/full/max/max/0/default.mp4");
            request.Headers.Add("Cookie", "dlcs-token-99=blabla;");
            var response = await httpClient.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Get_AssetRequiresAuth_ProxiesToS3_IfCookieTokenValid()
        {
            // Arrange
            var id = "99/1/cookie-pass";
            await dbFixture.DbContext.Images.AddTestAsset(id, family: AssetFamily.Timebased, mediaType: "video/mpeg",
                maxUnauthorised: 100, origin: "/test/space", roles: "clickthrough");
            var userSession =
                await dbFixture.DbContext.SessionUsers.AddTestSession(
                    DlcsDatabaseFixture.ClickThroughAuthService.AsList());
            var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(15),
                sessionUserId: userSession.Entity.Id);
            await dbFixture.DbContext.SaveChangesAsync();
            
            var expectedPath =
                new Uri(
                    "https://s3-eu-west-1.amazonaws.com/test-dlcs-storage/99/1/cookie-pass/full/full/max/max/0/default.mp4");

            // Act
            var request = new HttpRequestMessage(HttpMethod.Get,
                "/iiif-av/99/1/cookie-pass/full/full/max/max/0/default.mp4");
            request.Headers.Add("Cookie", $"dlcs-token-99=id={authToken.Entity.CookieId};");
            var response = await httpClient.SendAsync(request);
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();

            // Assert
            proxyResponse.Uri.Should().Be(expectedPath);
        }
        
        [Fact]
        public async Task Head_AssetRequiresAuth_Returns200_IfCookieValid()
        {
            // Arrange
            var id = "99/1/cookie-head";
            await dbFixture.DbContext.Images.AddTestAsset(id, family: AssetFamily.Timebased, mediaType: "video/mpeg",
                maxUnauthorised: 100, origin: "/test/space", roles: "clickthrough");
            var userSession =
                await dbFixture.DbContext.SessionUsers.AddTestSession(
                    DlcsDatabaseFixture.ClickThroughAuthService.AsList());
            var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(15),
                sessionUserId: userSession.Entity.Id);
            await dbFixture.DbContext.SaveChangesAsync();

            // Act
            var request = new HttpRequestMessage(HttpMethod.Head,
                "/iiif-av/99/1/cookie-head/full/full/max/max/0/default.mp4");
            request.Headers.Add("Cookie", $"dlcs-token-99=id={authToken.Entity.CookieId};");
            var response = await httpClient.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        
        [Fact]
        public async Task Head_AssetRequiresAuth_Returns401_IfCookieProvided_ButInvalid()
        {
            // Arrange
            var id = "99/1/cookie-head-invalid";
            await dbFixture.DbContext.Images.AddTestAsset(id, family: AssetFamily.Timebased, mediaType: "video/mpeg",
                maxUnauthorised: 100, origin: "/test/space", roles: "clickthrough");
            var userSession =
                await dbFixture.DbContext.SessionUsers.AddTestSession(
                    DlcsDatabaseFixture.ClickThroughAuthService.AsList());
            var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(-15),
                sessionUserId: userSession.Entity.Id);
            await dbFixture.DbContext.SaveChangesAsync();

            // Act
            var request = new HttpRequestMessage(HttpMethod.Head, $"/iiif-av/{id}/full/full/max/max/0/default.mp4");
            request.Headers.Add("Cookie", $"dlcs-token-99=id={authToken.Entity.CookieId};");
            var response = await httpClient.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}