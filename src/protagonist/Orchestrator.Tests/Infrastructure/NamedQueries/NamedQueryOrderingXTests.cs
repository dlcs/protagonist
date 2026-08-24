using System.Linq;
using System.Runtime.CompilerServices;
using DLCS.Core.Types;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Infrastructure.NamedQueries;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Data;
using Test.Helpers.Integration;
using QueryMapping = DLCS.Model.Assets.NamedQueries.ParsedNamedQuery.QueryMapping;
using OrderDirection = DLCS.Model.Assets.NamedQueries.ParsedNamedQuery.OrderDirection;
using QueryOrder = DLCS.Model.Assets.NamedQueries.ParsedNamedQuery.QueryOrder;

namespace Orchestrator.Tests.Infrastructure.NamedQueries;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class NamedQueryOrderingXTests
{
    private readonly DlcsContext dbContext;

    public NamedQueryOrderingXTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;

        dbFixture.CleanUp();
    }

    [Fact]
    public void OrderByNamedQuery_ReturnsQueryableUnaltered_IfAllOrderingsUnset()
    {
        // Arrange
        var query = GetParsedNamedQuery(new QueryOrder(QueryMapping.Unset));
        var assets = dbContext.Images.AsQueryable();

        // Act
        var ordered = assets.OrderByNamedQuery(query);

        // Assert
        ordered.Should().BeSameAs(assets);
    }

    [Theory]
    [InlineData(QueryMapping.Number1)]
    [InlineData(QueryMapping.Number2)]
    [InlineData(QueryMapping.Number3)]
    [InlineData(QueryMapping.String1)]
    [InlineData(QueryMapping.String2)]
    [InlineData(QueryMapping.String3)]
    public async Task OrderByNamedQuery_OrdersAscendingByMapping(QueryMapping queryMapping)
    {
        // Arrange
        var second = await AddAsset("-second", 2, 2, 2, "b", "b", "b");
        var third = await AddAsset("-third", 3, 3, 3, "c", "c", "c");
        var first = await AddAsset("-first", 1, 1, 1, "a", "a", "a");
        await dbContext.SaveChangesAsync();

        var query = GetParsedNamedQuery(new QueryOrder(queryMapping));

        // Act
        var ordered = await dbContext.Images.OrderByNamedQuery(query).ToListAsync();

        // Assert
        ordered.Select(a => a.Id).Should().Equal(first, second, third);
    }

    [Fact]
    public async Task OrderByNamedQuery_OrdersDescending()
    {
        // Arrange
        var second = await AddAsset("-second", num1: 2);
        var third = await AddAsset("-third", num1: 3);
        var first = await AddAsset("-first", num1: 1);
        await dbContext.SaveChangesAsync();

        var query = GetParsedNamedQuery(new QueryOrder(QueryMapping.Number1, OrderDirection.Descending));

        // Act
        var ordered = await dbContext.Images.OrderByNamedQuery(query).ToListAsync();

        // Assert
        ordered.Select(a => a.Id).Should().Equal(third, second, first);
    }

    [Fact]
    public async Task OrderByNamedQuery_OrdersByMultipleMappings_HonouringDirectionOfEach()
    {
        // Arrange - mirrors "assetOrder=n1;n2 desc;s1"
        var third = await AddAsset("-third", num1: 1, num2: 10, ref1: "z");
        var first = await AddAsset("-first", num1: 1, num2: 20, ref1: "c");
        var fourth = await AddAsset("-fourth", num1: 2, num2: 10, ref1: "a");
        var second = await AddAsset("-second", num1: 1, num2: 10, ref1: "x");
        await dbContext.SaveChangesAsync();

        var query = GetParsedNamedQuery(
            new QueryOrder(QueryMapping.Number1),
            new QueryOrder(QueryMapping.Number2, OrderDirection.Descending),
            new QueryOrder(QueryMapping.String1));

        // Act
        var ordered = await dbContext.Images.OrderByNamedQuery(query).ToListAsync();

        // Assert
        ordered.Select(a => a.Id).Should().Equal(first, second, third, fourth);
    }

    [Fact]
    public async Task OrderByNamedQuery_IgnoresUnsetMappings()
    {
        // Arrange - "assetOrder=unknownField;n1" parses to Unset followed by Number1
        var second = await AddAsset("-second", num1: 2);
        var first = await AddAsset("-first", num1: 1);
        await dbContext.SaveChangesAsync();

        var query = GetParsedNamedQuery(
            new QueryOrder(QueryMapping.Unset),
            new QueryOrder(QueryMapping.Number1),
            new QueryOrder(QueryMapping.Unset, OrderDirection.Descending));

        // Act
        var ordered = await dbContext.Images.OrderByNamedQuery(query).ToListAsync();

        // Assert
        ordered.Select(a => a.Id).Should().Equal(first, second);
    }

    private static ParsedNamedQuery GetParsedNamedQuery(params QueryOrder[] assetOrdering)
        => new(99) { AssetOrdering = assetOrdering.ToList() };

    private async Task<AssetId> AddAsset(string assetPostfix, int num1 = 0, int num2 = 0, int num3 = 0,
        string ref1 = "", string ref2 = "", string ref3 = "", [CallerMemberName] string caller = "")
    {
        var assetId = AssetIdGenerator.GetAssetId(asset: caller, assetPostfix: assetPostfix);
        await dbContext.Images.AddTestAsset(assetId, num1: num1, num2: num2, num3: num3, ref1: ref1, ref2: ref2,
            ref3: ref3);
        return assetId;
    }
}
