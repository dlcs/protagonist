using System.Collections.Generic;
using API.Features.Customer.Validation;
using API.Settings;
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
        var adjuncts = new Dictionary<string, List<string>>
        {
            {AssetIdGenerator.GetAssetId().ToString(), ["first"]}
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldNotHaveAnyValidationErrors();
    }
    
    [Fact]
    public void Valid_WhenMultipleAdjunctSingleAsset()
    {
        var adjuncts = new Dictionary<string, List<string>>
        {
            {AssetIdGenerator.GetAssetId().ToString(), ["first", "second"]}
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldNotHaveAnyValidationErrors();
    }
    
    [Fact]
    public void Valid_WhenSingleAdjunctMultipleAsset()
    {
        var adjuncts = new Dictionary<string, List<string>>
        {
            {AssetIdGenerator.GetAssetId().ToString(), ["first"]},
            {AssetIdGenerator.GetAssetId(assetPostfix: "_1").ToString(), ["first"]}
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldNotHaveAnyValidationErrors();
    }
    
    [Fact]
    public void Invalid_WhenNullValue()
    {
        Dictionary<string, List<string>> adjuncts = null;
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldHaveAnyValidationError();
    }
    
    [Fact]
    public void Invalid_WhenEmptyValue()
    {
        Dictionary<string, List<string>> adjuncts = [];
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldHaveAnyValidationError();
    }
    
    [Fact]
    public void Invalid_WhenMultipleAdjunctRepeated()
    {
        var assetId = AssetIdGenerator.GetAssetId().ToString();
        var adjuncts = new Dictionary<string, List<string>>
        {
            {assetId, ["first", "first"]}
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldHaveAnyValidationError().WithErrorMessage($"Members contains 1 duplicate Id(s): asset Id: {assetId} : adjunct id: first");
    }
    
    [Fact]
    public void Invalid_WhenMoreAdjunctsThanBatchSize()
    {
        var assetId = AssetIdGenerator.GetAssetId().ToString();
        var adjuncts = new Dictionary<string, List<string>>
        {
            {assetId, ["first", "second", "third", "fourth", "fifth"]}
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldHaveAnyValidationError().WithErrorMessage("Maximum adjuncts in single batch is 4");
    }
    
    [Fact]
    public void Invalid_WhenMoreSingleAdjunctAssetsThanBatchSize()
    {
        var adjuncts = new Dictionary<string, List<string>>
        {
            {AssetIdGenerator.GetAssetId().ToString(), ["first"]},
            {AssetIdGenerator.GetAssetId(assetPostfix: "_1").ToString(), ["second"]},
            {AssetIdGenerator.GetAssetId(assetPostfix: "_2").ToString(), ["third"]},
            {AssetIdGenerator.GetAssetId(assetPostfix: "_3").ToString(), ["fourth"]},
            {AssetIdGenerator.GetAssetId(assetPostfix: "_4").ToString(), ["fifth"]},
        };
        
        var result = sut.TestValidate(adjuncts);
        result.ShouldHaveAnyValidationError().WithErrorMessage("Maximum adjuncts in single batch is 4");
    }
}
