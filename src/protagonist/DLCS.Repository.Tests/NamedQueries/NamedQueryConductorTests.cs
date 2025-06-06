﻿using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository.NamedQueries;
using DLCS.Repository.NamedQueries.Parsing;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace DLCS.Repository.Tests.NamedQueries;

public class NamedQueryConductorTests
{
    private readonly INamedQueryRepository namedQueryRepository;
    private readonly INamedQueryParser namedQueryParser;
    private const int Customer = 99;
    private readonly NamedQueryConductor sut;

    public NamedQueryConductorTests()
    {
        namedQueryRepository = A.Fake<INamedQueryRepository>();
        namedQueryParser = A.Fake<INamedQueryParser>();
        sut = new NamedQueryConductor(namedQueryRepository, _ => namedQueryParser,
            new NullLogger<NamedQueryConductor>());
    }

    [Fact]
    public async Task GetNamedQueryResult_ReturnsEmptyResult_IfNamedQueryNotFound()
    {
        // Arrange
        const string queryName = "my-query";
        A.CallTo(() => namedQueryRepository.GetByName(Customer, queryName, true)).Returns<NamedQuery?>(null);
        
        // Act
        var result = await sut.GetNamedQueryResult<IIIFParsedNamedQuery>(queryName, Customer, null);
        
        // Assert
        result.ParsedQuery.Should().BeNull();
        result.Results.Should().BeEmpty();
    }
    
    [Fact]
    public async Task GetNamedQueryResult_ReturnsParseQueryWithNoResults_IfFaulted()
    {
        // Arrange
        const string queryName = "my-query";
        const string args = "/123";
        var namedQuery = new NamedQuery { Template = "s1=p2", Name = "test-query"};
        var faultedQuery = new IIIFParsedNamedQuery(Customer);
        faultedQuery.SetError("Test Error");
        A.CallTo(() => namedQueryRepository.GetByName(Customer, queryName, true))
            .Returns(namedQuery);
        A.CallTo(() =>
                namedQueryParser.GenerateParsedNamedQueryFromRequest<IIIFParsedNamedQuery>(Customer, args,
                    namedQuery.Template, namedQuery.Name))
            .Returns(faultedQuery);
        
        // Act
        var result = await sut.GetNamedQueryResult<IIIFParsedNamedQuery>(queryName, Customer,args);
        
        // Assert
        result.ParsedQuery.IsFaulty.Should().BeTrue();
        result.Results.Should().BeEmpty();
    }
    
    [Fact]
    public async Task GetNamedQueryResult_ReturnsMatchingImages_AndParsedQueryIfSuccessful()
    {
        // Arrange
        const string queryName = "my-query";
        const string args = "/123";
        var namedQuery = new NamedQuery { Template = "s1=p2", Name = "test-query" };
        var parsedQuery = new IIIFParsedNamedQuery(Customer);
        var images = new List<Asset> { new(AssetId.FromString("1/1/my-image")) };
        A.CallTo(() => namedQueryRepository.GetByName(Customer, queryName, true))
            .Returns(namedQuery);
        A.CallTo(() =>
                namedQueryParser.GenerateParsedNamedQueryFromRequest<IIIFParsedNamedQuery>(Customer, args,
                    namedQuery.Template, namedQuery.Name))
            .Returns(parsedQuery);
        A.CallTo(() => namedQueryRepository.GetNamedQueryResults(parsedQuery)).Returns(images.AsQueryable());
        
        // Act
        var result = await sut.GetNamedQueryResult<IIIFParsedNamedQuery>(queryName, Customer, args);
        
        // Assert
        result.ParsedQuery.Should().Be(parsedQuery);
        result.Results.Should().BeEquivalentTo(images);
    }
}