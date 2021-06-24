using System.Net.Http;
using System.Threading.Tasks;
using DLCS.Repository;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Portal.Tests.Integration.Infrastructure;
using Xunit;

namespace Portal.Tests.Integration
{
    [Trait("Category", "Integration")]
    [Collection(DatabaseCollection.CollectionName)]
    public class BasicTests : IClassFixture<ProtagonistAppFactory<Startup>>
    {
        private readonly DlcsContext dbContext;
        private readonly HttpClient httpClient;

        public BasicTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
        {
            dbContext = dbFixture.DbContext;
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
    }
}