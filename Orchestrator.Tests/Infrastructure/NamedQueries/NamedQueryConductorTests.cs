using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Orchestrator.Infrastructure.NamedQueries;
using Xunit;

namespace Orchestrator.Tests.Infrastructure.NamedQueries
{
    public class NamedQueryConductorTests
    {
        private readonly INamedQueryRepository namedQueryRepository;
        private readonly INamedQueryParser namedQueryParser;
        private static readonly CustomerPathElement Customer = new(99, "test-customer");
        private readonly NamedQueryConductor sut;

        public NamedQueryConductorTests()
        {
            namedQueryRepository = A.Fake<INamedQueryRepository>();
            namedQueryParser = A.Fake<INamedQueryParser>();
            sut = new NamedQueryConductor(namedQueryRepository, namedQueryParser,
                new NullLogger<NamedQueryConductor>());
        }

        [Fact]
        public async Task GetNamedQueryResult_ReturnsEmptyResult_IfNamedQueryNotFound()
        {
            // Arrange
            const string queryName = "my-query";
            A.CallTo(() => namedQueryRepository.GetByName(Customer.Id, queryName, true)).Returns<NamedQuery?>(null);
            
            // Act
            var result = await sut.GetNamedQueryResult(queryName, Customer, null);
            
            // Assert
            result.Query.Should().BeNull();
            result.Results.Should().BeEmpty();
        }
        
        [Fact]
        public async Task GetNamedQueryResult_ReturnsParseQueryWithNoResults_IfFaulted()
        {
            // Arrange
            const string queryName = "my-query";
            const string args = "/123";
            var namedQuery = new NamedQuery { Template = "s1=p2" };
            var faultedQuery = new ParsedNamedQuery(Customer);
            faultedQuery.SetError("Test Error");
            A.CallTo(() => namedQueryRepository.GetByName(Customer.Id, queryName, true))
                .Returns(namedQuery);
            A.CallTo(() => namedQueryParser.GenerateParsedNamedQueryFromRequest(Customer, args, namedQuery.Template))
                .Returns(faultedQuery);
            
            // Act
            var result = await sut.GetNamedQueryResult(queryName, Customer, args);
            
            // Assert
            result.Query.IsFaulty.Should().BeTrue();
            result.Results.Should().BeEmpty();
        }
        
        [Fact]
        public async Task GetNamedQueryResult_ReturnsMatchingImages_AndParsedQueryIfSuccessful()
        {
            // Arrange
            const string queryName = "my-query";
            const string args = "/123";
            var namedQuery = new NamedQuery { Template = "s1=p2" };
            var parsedQuery = new ParsedNamedQuery(Customer);
            var images = new List<Asset> { new() { Id = "/1/1/my-image" } };
            A.CallTo(() => namedQueryRepository.GetByName(Customer.Id, queryName, true))
                .Returns(namedQuery);
            A.CallTo(() => namedQueryParser.GenerateParsedNamedQueryFromRequest(Customer, args, namedQuery.Template))
                .Returns(parsedQuery);
            A.CallTo(() => namedQueryRepository.GetNamedQueryResults(parsedQuery)).Returns(images);
            
            // Act
            var result = await sut.GetNamedQueryResult(queryName, Customer, args);
            
            // Assert
            result.Query.Should().Be(parsedQuery);
            result.Results.Should().BeEquivalentTo(images);
        }
    }
}