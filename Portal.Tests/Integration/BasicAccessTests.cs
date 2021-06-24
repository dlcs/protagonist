using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Portal.Tests.Integration.Infrastructure;
using Xunit;

namespace Portal.Tests.Integration
{
    [Trait("Category", "Integration")]
    [Collection(DatabaseCollection.CollectionName)]
    public class BasicAccessTests : IClassFixture<ProtagonistAppFactory<Startup>>
    {
        private readonly HttpClient httpClient;

        public BasicAccessTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
        {
            httpClient = factory
                .WithConnectionString(dbFixture.ConnectionString)
                .CreateClient(new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false
                });
        }

        [Theory]
        [InlineData("/Account/Login")]
        [InlineData("/Account/SignedOut")]
        [InlineData("/AccessDenied")]
        [InlineData("/Error")]
        [InlineData("/Index")]
        public async Task Get_AnonymousPages_ReturnsSuccessAndCorrectContentType(string url)
        {
            // Act
            var response = await httpClient.GetAsync(url);
            
            // Assert
            response.EnsureSuccessStatusCode();
            response.Content.Headers.ContentType.ToString().Should().Be("text/html; charset=utf-8");
        }
        
        [Theory]
        [InlineData("/Images")]
        [InlineData("/Keys")]
        [InlineData("/Keys/Create")]
        [InlineData("/Users")]
        [InlineData("/Admin")]
        public async Task Get_AuthPages_Returns401(string url)
        {
            // Act
            var response = await httpClient.GetAsync(url);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        
        [Theory]
        [InlineData("/Admin")]
        public async Task Get_AdminPages_LoggedInAsCustomer_Returns403(string url)
        {
            // Act
            var response = await httpClient.AsCustomer().GetAsync(url);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        
        [Theory]
        [InlineData("/Admin")]
        public async Task Get_AdminPages_LoggedInAsAdmin_ReturnsSuccessAndCorrectContentType(string url)
        {
            // Act
            var response = await httpClient.AsAdmin().GetAsync(url);

            // Assert
            response.EnsureSuccessStatusCode();
            response.Content.Headers.ContentType.ToString().Should().Be("text/html; charset=utf-8");
        }
    }
}