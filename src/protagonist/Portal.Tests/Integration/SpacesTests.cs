using System;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using DLCS.Model.Spaces;
using DLCS.Repository;
using FluentAssertions;
using Portal.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;
using Xunit;

namespace Portal.Tests.Integration
{
    [Trait("Category", "Integration")]
    [Collection(DatabaseCollection.CollectionName)]
    public class SpacesTests : IClassFixture<ProtagonistAppFactory<Startup>>
    {
        private readonly DlcsContext dbContext;
        private readonly HttpClient httpClient;

        public SpacesTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
        {
            dbContext = dbFixture.DbContext;
            httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(dbFixture, "Test");
            dbFixture.CleanUp();
        }
        
        [Fact(Skip = "Requires onward testing of API")]
        public async Task Get_ReturnsPage_IfNoSpacesForCustomer()
        {
            // Arrange
            httpClient.AsCustomer();

            await dbContext.SaveChangesAsync();

            // Act
            var response = await httpClient.GetAsync("/spaces");
            var htmlParser = new HtmlParser();
            var document = htmlParser.ParseDocument(await response.Content.ReadAsStreamAsync());
            var table = document.QuerySelector("table.table") as IHtmlTableElement;
            
            // Assert
            table.Rows.Length.Should().Be(1);
        }

        [Fact(Skip = "Requires onward testing of API")]
        public async Task Get_ReturnsAllSpacesForCustomer()
        {
            // Arrange
            // Add 3 spaces - 2 for this customer and 1 for another
            await dbContext.Spaces.AddRangeAsync(
                new Space {Customer = 1, Id = 1, Created = DateTime.UtcNow, Name = "space1"},
                new Space {Customer = 2, Id = 2, Created = DateTime.UtcNow, Name = "space2"},
                new Space {Customer = 2, Id = 3, Created = DateTime.UtcNow, Name = "space3"}
            );
            
            await dbContext.SaveChangesAsync();

            // Act
            var response = await httpClient.AsCustomer().GetAsync("/spaces");
            var htmlParser = new HtmlParser();
            var document = htmlParser.ParseDocument(await response.Content.ReadAsStreamAsync());
            var table = document.QuerySelector("table.table") as IHtmlTableElement;
            
            // Assert
            // There should be 'expected rows + 1' as one will be header
            table.Rows.Length.Should().Be(3);
        }
    }
}