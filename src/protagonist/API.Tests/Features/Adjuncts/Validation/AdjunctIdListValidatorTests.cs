using System.Collections.Generic;
using System.Linq;
using API.Features.Customer.Validation;
using API.Settings;
using DLCS.Core.Types;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;
using Test.Helpers.Data;

namespace API.Tests.Features.Adjuncts.Validation;

public class AdjunctIdListValidatorTests
{
    private readonly AdjunctIdListValidator sut = new(Options.Create(
        new ApiSettings
        {
            MaxImageListSize = 4,
            RestrictedResourceIdCharacterString = "\\ /"
        }));
    
    [Fact]
    public void Valid_WhenSingleAdjunct()
    {
        var adjuncts = new Dictionary<AssetId, List<string>>
        {
            {AssetIdGenerator.GetAssetId(), ["first"]}
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldNotHaveAnyValidationErrors();
    }
    
    [Fact]
    public void Valid_WhenMultipleAdjunctSingleAsset()
    {
        var adjuncts = new Dictionary<AssetId, List<string>>
        {
            {AssetIdGenerator.GetAssetId(), ["first", "second"]}
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldNotHaveAnyValidationErrors();
    }
    
    [Fact]
    public void Valid_WhenSingleAdjunctMultipleAsset()
    {
        var adjuncts = new Dictionary<AssetId, List<string>>
        {
            {AssetIdGenerator.GetAssetId(), ["first"]},
            {AssetIdGenerator.GetAssetId(assetPostfix: "_1"), ["first"]}
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldNotHaveAnyValidationErrors();
    }
    
    [Fact]
    public void Invalid_WhenNullValue()
    {
        Dictionary<AssetId, List<string>> adjuncts = null;
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldHaveValidationErrors();
    }
    
    [Fact]
    public void Invalid_WhenEmptyValue()
    {
        Dictionary<AssetId, List<string>> adjuncts = [];
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldHaveValidationErrors();
    }
    
    [Fact]
    public void Invalid_WhenMultipleAdjunctRepeated()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        var adjuncts = new Dictionary<AssetId, List<string>>
        {
            {assetId, ["first", "first"]}
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldHaveValidationErrors().WithErrorMessage($"Members contains 1 duplicate Id(s): asset Id: {assetId} : adjunct id: first");
    }
    
    [Fact]
    public void Invalid_WhenMultipleAdjunctRepeatedWithAdditional()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        var adjuncts = new Dictionary<AssetId, List<string>>
        {
            {assetId, ["first", "first", "second"]}
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldHaveValidationErrors().WithErrorMessage($"Members contains 1 duplicate Id(s): asset Id: {assetId} : adjunct id: first");
    }
    
    [Fact]
    public void Invalid_WhenMultipleAdjunctRepeatedWithSecondAdjunct()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        var adjuncts = new Dictionary<AssetId, List<string>>
        {
            {assetId, ["first", "first"]},
            {AssetIdGenerator.GetAssetId(assetPostfix: "_1"), ["second"]}
        };

        var stuff3 = adjuncts.Any(kvp =>
            kvp.Value.SelectMany(a => a).Distinct().Count() == kvp.Value.SelectMany(a => a).Count());
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldHaveValidationErrors().WithErrorMessage($"Members contains 1 duplicate Id(s): asset Id: {assetId} : adjunct id: first");
    }
    
    [Fact]
    public void Invalid_WhenMoreAdjunctsThanBatchSize()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        var adjuncts = new Dictionary<AssetId, List<string>>
        {
            {assetId, ["first", "second", "third", "fourth", "fifth"]}
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldHaveValidationErrors().WithErrorMessage("Maximum adjuncts in single batch is 4");
    }
    
    [Fact]
    public void Invalid_WhenMoreSingleAdjunctAssetsThanBatchSize()
    {
        var adjuncts = new Dictionary<AssetId, List<string>>
        {
            {AssetIdGenerator.GetAssetId(), ["first"]},
            {AssetIdGenerator.GetAssetId(assetPostfix: "_1"), ["second"]},
            {AssetIdGenerator.GetAssetId(assetPostfix: "_2"), ["third"]},
            {AssetIdGenerator.GetAssetId(assetPostfix: "_3"), ["fourth"]},
            {AssetIdGenerator.GetAssetId(assetPostfix: "_4"), ["fifth"]},
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldHaveValidationErrors().WithErrorMessage("Maximum adjuncts in single batch is 4");
    }
    
    [Theory]
    [InlineData(" space")]
    [InlineData("slash\\")]
    [InlineData("other/slash")]
    public void Invalid_WhenAdjunctContainsInvalidCharacters(string invalidId)
    {
        var assetId = AssetIdGenerator.GetAssetId();
        var adjuncts = new Dictionary<AssetId, List<string>>
        {
            {assetId, ["first", "second", invalidId]}
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldHaveValidationErrors()
            .WithErrorMessage(
                "Adjunct id contains at least one of the following restricted characters. Invalid values are: \\ /");
    }
}
