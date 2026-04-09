using System;
using System.Collections.Generic;
using API.Exceptions;
using API.Features.Customer.Infrastructure;
using DLCS.Model;
using Test.Helpers.Data;

namespace API.Tests.Features.Adjuncts.Infrastructure;

public class AdjunctIdentifierOnlyXTests
{
    [Fact]
    public void ConvertToDictionary_ConvertsSingleAdjunctIdentifier()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        
        List<AdjunctAssetIdentifier> adjunctIdentifiers =
        [
            new ()
            {
                Id = assetId.ToString(),
                Adjunct = [ "first", "second" ]
            }
        ];

        // Act
        var adjunctDictionary = adjunctIdentifiers.ConvertToDictionary(assetId.Customer);

        // Assert
        adjunctDictionary.Keys.Should().Contain(assetId);
        adjunctDictionary.Keys.Count.Should().Be(adjunctIdentifiers.Count);
        adjunctDictionary.Values.Should().Contain(adjunctIdentifiers[0].Adjunct);
    }
    
    [Fact]
    public void ConvertToDictionary_ConvertsMultipleAdjunctIdentifier()
    {
        // Arrange
        var firstAssetId = AssetIdGenerator.GetAssetId();
        var secondAssetId = AssetIdGenerator.GetAssetId(assetPostfix: "_1");
        
        List<AdjunctAssetIdentifier> adjunctIdentifiers =
        [
            new ()
            {
                Id = firstAssetId.ToString(),
                Adjunct = [ "first", "second" ]
            },
            new ()
            {
                Id = secondAssetId.ToString(),
                Adjunct = [ "third", "fourth" ]
            }
        ];

        // Act
        var adjunctDictionary = adjunctIdentifiers.ConvertToDictionary(firstAssetId.Customer);

        // Assert
        adjunctDictionary.Keys.Count.Should().Be(adjunctIdentifiers.Count);
        adjunctDictionary.Keys.Should().Contain(firstAssetId);
        adjunctDictionary.Values.Should().Contain(adjunctIdentifiers[0].Adjunct);
        adjunctDictionary.Keys.Should().Contain(secondAssetId);
        adjunctDictionary.Values.Should().Contain(adjunctIdentifiers[1].Adjunct);
    }
    
    [Fact]
    public void ConvertToDictionary_ConcatenatesMultipleAdjunctIdentifier()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        
        List<AdjunctAssetIdentifier> adjunctIdentifiers =
        [
            new ()
            {
                Id = assetId.ToString(),
                Adjunct = [ "first", "second" ]
            },
            new ()
            {
                Id = assetId.ToString(),
                Adjunct = [ "third", "fourth" ]
            }
        ];

        // Act
        var adjunctDictionary = adjunctIdentifiers.ConvertToDictionary(assetId.Customer);

        // Assert
        var fullValues = adjunctIdentifiers[0].Adjunct;
        fullValues.AddRange(adjunctIdentifiers[1].Adjunct);
        
        adjunctDictionary.Keys.Count.Should().Be(1);
        adjunctDictionary.Keys.Should().Contain(assetId);
        adjunctDictionary.Values.Should().Contain(fullValues);
    }
    
    [Fact]
    public void ConvertToDictionary_ThrowsError_WhenAdjunctFailsToParse()
    {
        // Arrange
        List<AdjunctAssetIdentifier> adjunctIdentifiers =
        [
            new ()
            {
                Id = "notAnAssetId",
                Adjunct = [ "first", "second" ]
            }
        ];

        // Act
        Action action = () => adjunctIdentifiers.ConvertToDictionary(1);

        // Assert
        action.Should().Throw<BadRequestException>().WithMessage("AssetId 'notAnAssetId' is invalid. Must be in format customer/space/asset");
    }
    
    [Fact]
    public void ConvertToDictionary_ThrowsError_WhenDifferentCustomerId()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        
        List<AdjunctAssetIdentifier> adjunctIdentifiers =
        [
            new ()
            {
                Id = assetId.ToString(),
                Adjunct = [ "first", "second" ]
            }
        ];

        // Act
        Action action = () => adjunctIdentifiers.ConvertToDictionary(1);

        // Assert
        action.Should().Throw<BadRequestException>().WithMessage($"Asset id '{assetId}' cannot belong to a different customer");
    }
}
