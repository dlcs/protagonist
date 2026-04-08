using System.Collections.Generic;
using System.Linq;
using API.Features.Adjuncts.Infrastructure;
using DLCS.Model;

namespace API.Tests.Features.Adjuncts.Infrastructure;

public class AdjunctIdentifierOnlyXTests
{
    [Fact]
    public void ConvertToDictionary_ConvertsSingleAdjunctIdentifier()
    {
        // Arrange
        List<AdjunctIdentifierOnly> adjunctIdentifiers =
        [
            new ()
            {
                Id = "first",
                Adjunct = [ "first", "second" ]
            }
        ];

        // Act
        var adjunctDictionary = adjunctIdentifiers.ConvertToDictionary();

        // Assert
        adjunctDictionary.Keys.Should().Contain(adjunctIdentifiers[0].Id);
        adjunctDictionary.Keys.Count.Should().Be(adjunctIdentifiers.Count);
        adjunctDictionary.Values.Should().Contain(adjunctIdentifiers[0].Adjunct);
    }
    
    [Fact]
    public void ConvertToDictionary_ConvertsMultipleAdjunctIdentifier()
    {
        // Arrange
        List<AdjunctIdentifierOnly> adjunctIdentifiers =
        [
            new ()
            {
                Id = "first",
                Adjunct = [ "first", "second" ]
            },
            new ()
            {
                Id = "second",
                Adjunct = [ "third", "fourth" ]
            }
        ];

        // Act
        var adjunctDictionary = adjunctIdentifiers.ConvertToDictionary();

        // Assert
        adjunctDictionary.Keys.Count.Should().Be(adjunctIdentifiers.Count);
        adjunctDictionary.Keys.Should().Contain(adjunctIdentifiers[0].Id);
        adjunctDictionary.Values.Should().Contain(adjunctIdentifiers[0].Adjunct);
        adjunctDictionary.Keys.Should().Contain(adjunctIdentifiers[1].Id);
        adjunctDictionary.Values.Should().Contain(adjunctIdentifiers[1].Adjunct);
    }
    
    [Fact]
    public void ConvertToDictionary_ConcatenatesMultipleAdjunctIdentifier()
    {
        // Arrange
        List<AdjunctIdentifierOnly> adjunctIdentifiers =
        [
            new ()
            {
                Id = "first",
                Adjunct = [ "first", "second" ]
            },
            new ()
            {
                Id = "first",
                Adjunct = [ "third", "fourth" ]
            }
        ];

        // Act
        var adjunctDictionary = adjunctIdentifiers.ConvertToDictionary();

        // Assert
        var fullValues = adjunctIdentifiers[0].Adjunct;
        fullValues.AddRange(adjunctIdentifiers[1].Adjunct);
        
        adjunctDictionary.Keys.Count.Should().Be(1);
        adjunctDictionary.Keys.Should().Contain(adjunctIdentifiers[0].Id);
        adjunctDictionary.Values.Should().Contain(fullValues);
    }
    
    [Fact]
    public void Flatten_ConvertsSingleAdjunctIdentifier()
    {
        // Arrange
        List<AdjunctIdentifierOnly> adjunctIdentifiers =
        [
            new ()
            {
                Id = "first",
                Adjunct = [ "first", "second" ]
            }
        ];

        // Act
        var adjuncts = adjunctIdentifiers.Flatten().ToList();

        // Assert
        adjuncts.Count.Should().Be(2);
        adjuncts.First().Should().Be(new KeyValuePair<string,string>("first", "first"));
        adjuncts.Last().Should().Be(new KeyValuePair<string,string>("first", "second"));
    }
    
    [Fact]
    public void Flatten_ConvertsMultipleAdjunctIdentifier()
    {
        // Arrange
        List<AdjunctIdentifierOnly> adjunctIdentifiers =
        [
            new ()
            {
                Id = "first",
                Adjunct = [ "first", "second" ]
            },
            new ()
            {
                Id = "second",
                Adjunct = [ "third", "fourth" ]
            }
        ];

        // Act
        var adjuncts = adjunctIdentifiers.Flatten().ToList();

        // Assert
        adjuncts.Count.Should().Be(4);
        adjuncts.First().Should().Be(new KeyValuePair<string,string>("first", "first"));
        adjuncts[1].Should().Be(new KeyValuePair<string,string>("first", "second"));
        adjuncts[2].Should().Be(new KeyValuePair<string,string>("second", "third"));
        adjuncts.Last().Should().Be(new KeyValuePair<string,string>("second", "fourth"));
    }
}
