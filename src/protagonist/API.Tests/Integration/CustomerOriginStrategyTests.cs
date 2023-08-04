using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using API.Tests.Integration.Infrastructure;
using DLCS.Model.Customers;
using DLCS.Repository;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class CustomerOriginStrategyTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    private readonly DlcsContext dlcsContext;

    public CustomerOriginStrategyTests(DlcsDatabaseFixture dbFixture, ProtagonistAppFactory<Startup> factory)
    {
        dlcsContext = dbFixture.DbContext;
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(dbFixture, "API-Test");
        dbFixture.CleanUp();
    }

    [Fact]
    public async Task Get_CustomerOriginStrategy_200()
    {
        // Arrange
        const int customerId = 90;
        var strategy = new CustomerOriginStrategy()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Regex = "http[s]?://(.*).example.com",
            Strategy = OriginStrategyType.BasicHttp,
            Credentials = @"{""user"": ""test-user"",
                             ""password"": ""test-password""
                            }",
            Optimised = false,
            Order = 1
        };
        var path = $"customers/{customerId}/originStrategies/{strategy.Id}";
        
        await dlcsContext.CustomerOriginStrategies.AddAsync(strategy);
        await dlcsContext.SaveChangesAsync();

        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}