using API.Features.Customer.Validation;
using API.Settings;
using DLCS.Model;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;
using Test.Helpers.Data;

namespace API.Tests.Features.Adjuncts.Validation;

public class AdjunctIdListValidatorTests
{
    private readonly AdjunctIdListValidator sut = new(Options.Create(
        new ApiSettings
        {
            MaxImageListSize = 4
        }));
    
    [Fact]
    public void Valid_WhenSingleAdjunct()
    {
        var adjuncts = new AdjunctIdentifierOnly[1];
        adjuncts[0] = new AdjunctIdentifierOnly
        {
            Id = AssetIdGenerator.GetAssetId().ToString(),
            Adjunct = ["first"]
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldNotHaveAnyValidationErrors();
    }
    
    [Fact]
    public void Valid_WhenMultipleAdjunctSingleAsset()
    {
        var adjuncts = new AdjunctIdentifierOnly[1];
        adjuncts[0] = new AdjunctIdentifierOnly
        {
            Id = AssetIdGenerator.GetAssetId().ToString(),
            Adjunct = ["first", "second"]
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldNotHaveAnyValidationErrors();
    }
    
    [Fact]
    public void Valid_WhenSingleAdjunctMultipleAsset()
    {
        var adjuncts = new AdjunctIdentifierOnly[2];
        adjuncts[0] = new AdjunctIdentifierOnly
        {
            Id = AssetIdGenerator.GetAssetId().ToString(),
            Adjunct = ["first"]
        };
        adjuncts[1] = new AdjunctIdentifierOnly
        {
            Id = AssetIdGenerator.GetAssetId(asset: $"{nameof(Valid_WhenSingleAdjunctMultipleAsset)}_1").ToString(),
            Adjunct = ["first"]
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldNotHaveAnyValidationErrors();
    }
    
    [Fact]
    public void Invalid_WhenNullValue()
    {
        AdjunctIdentifierOnly[] adjuncts = null;
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldHaveAnyValidationError();
    }
    
    [Fact]
    public void Invalid_WhenEmptyValue()
    {
        AdjunctIdentifierOnly[] adjuncts = [];
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldHaveAnyValidationError();
    }
    
    [Fact]
    public void Invalid_WhenMultipleAdjunctRepeated()
    {
        var assetId = AssetIdGenerator.GetAssetId().ToString();
        var adjuncts = new AdjunctIdentifierOnly[1];
        adjuncts[0] = new AdjunctIdentifierOnly
        {
            Id = assetId,
            Adjunct = ["first", "first"]
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldHaveAnyValidationError().WithErrorMessage($"Members contains 1 duplicate Id(s): asset Id: {assetId} : adjunct id: first");
    }
    
    [Fact]
    public void Invalid_WhenMultipleAdjunctRepeatedAcrossMultipleDeclarations()
    {
        var assetId = AssetIdGenerator.GetAssetId().ToString();
        var adjuncts = new AdjunctIdentifierOnly[2];
        adjuncts[0] = new AdjunctIdentifierOnly
        {
            Id = assetId,
            Adjunct = ["first"]
        };
        adjuncts[1] = new AdjunctIdentifierOnly
        {
            Id = assetId,
            Adjunct = ["first"]
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldHaveAnyValidationError().WithErrorMessage($"Members contains 1 duplicate Id(s): asset Id: {assetId} : adjunct id: first");
    }
    
    [Fact]
    public void Invalid_WhenMoreAdjunctsThanBatchSize()
    {
        var assetId = AssetIdGenerator.GetAssetId().ToString();
        var adjuncts = new AdjunctIdentifierOnly[1];
        adjuncts[0] = new AdjunctIdentifierOnly
        {
            Id = assetId,
            Adjunct = ["first", "second", "third", "fourth", "fifth"]
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldHaveAnyValidationError().WithErrorMessage("Maximum adjuncts in single batch is 4");
    }
    
    [Fact]
    public void Invalid_WhenMoreSingleAdjunctAssetsThanBatchSize()
    {
        var adjuncts = new AdjunctIdentifierOnly[5];
        adjuncts[0] = new AdjunctIdentifierOnly
        {
            Id = AssetIdGenerator.GetAssetId().ToString(),
            Adjunct = ["first"]
        };
        adjuncts[1] = new AdjunctIdentifierOnly
        {
            Id = AssetIdGenerator.GetAssetId(asset: $"{nameof(Valid_WhenSingleAdjunctMultipleAsset)}_1").ToString(),
            Adjunct = ["first"]
        };
        adjuncts[2] = new AdjunctIdentifierOnly
        {
            Id = AssetIdGenerator.GetAssetId(asset: $"{nameof(Valid_WhenSingleAdjunctMultipleAsset)}_2").ToString(),
            Adjunct = ["first"]
        };
        adjuncts[3] = new AdjunctIdentifierOnly
        {
            Id = AssetIdGenerator.GetAssetId(asset: $"{nameof(Valid_WhenSingleAdjunctMultipleAsset)}_3").ToString(),
            Adjunct = ["first"]
        };
        adjuncts[4] = new AdjunctIdentifierOnly
        {
            Id = AssetIdGenerator.GetAssetId(asset: $"{nameof(Valid_WhenSingleAdjunctMultipleAsset)}_4").ToString(),
            Adjunct = ["first"]
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldHaveAnyValidationError().WithErrorMessage("Maximum adjuncts in single batch is 4");
    }
}
