using API.Features.Adjuncts.Validation;
using API.Features.AdjunctQueues.Validation;
using API.Settings;
using DLCS.HydraModel;
using FluentValidation.TestHelper;
using Hydra.Collections;
using Microsoft.Extensions.Options;

namespace API.Tests.Features.AdjunctQueues.Validation;

public class AdjunctBatchPostValidatorTests
{
    private readonly HydraAdjunctValidator adjunctValidator = new(Options.Create(
        new ApiSettings { RestrictedResourceIdCharacterString = "\\ /" }));

    private AdjunctBatchPostValidator GetSut(int maxBatchSize = 4) =>
        new(Options.Create(new ApiSettings { MaxBatchSize = maxBatchSize }), adjunctValidator);

    private static Adjunct ValidAdjunct(string modelId = "adjunct-1", string asset = "1/1/asset-1") =>
        new()
        {
            ModelId = modelId,
            Asset = asset,
            MediaType = "image/jpeg",
            IIIFLink = "seeAlso",
            Type = "Image",
            ExternalId = "https://example.org/some-id"
        };

    [Fact]
    public void Members_Null()
    {
        var sut = GetSut();
        var model = new HydraCollection<Adjunct>();
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(r => r.Members);
    }

    [Fact]
    public void Members_Empty()
    {
        var sut = GetSut();
        var model = new HydraCollection<Adjunct> { Members = [] };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(r => r.Members);
    }

    [Fact]
    public void Members_GreaterThanMaxBatchSize()
    {
        var sut = GetSut(maxBatchSize: 2);
        var model = new HydraCollection<Adjunct>
        {
            Members = [ValidAdjunct("a"), ValidAdjunct("b"), ValidAdjunct("c")]
        };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(r => r.Members)
            .WithErrorMessage("Maximum adjuncts in single batch is 2");
    }

    [Fact]
    public void Members_EqualToMaxBatchSize_Valid()
    {
        var sut = GetSut(maxBatchSize: 2);
        var model = new HydraCollection<Adjunct>
        {
            Members = [ValidAdjunct("a"), ValidAdjunct("b")]
        };
        var result = sut.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(r => r.Members);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Members_MissingAssetField(string? asset)
    {
        var sut = GetSut();
        var model = new HydraCollection<Adjunct>
        {
            Members = [ValidAdjunct(asset: asset!)]
        };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(r => r.Members)
            .WithErrorMessage("All members must have an 'asset' field");
    }

    [Fact]
    public void Members_OneOfSeveralMissingAsset_Fails()
    {
        var sut = GetSut();
        var model = new HydraCollection<Adjunct>
        {
            Members = [ValidAdjunct("a"), ValidAdjunct("b", asset: null!)]
        };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(r => r.Members)
            .WithErrorMessage("All members must have an 'asset' field");
    }

    [Fact]
    public void Members_DuplicateAssetAndModelId()
    {
        var sut = GetSut();
        var model = new HydraCollection<Adjunct>
        {
            Members =
            [
                ValidAdjunct("adjunct-1", "1/1/asset-a"),
                ValidAdjunct("adjunct-1", "1/1/asset-a"),
            ]
        };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor(r => r.Members)
            .WithErrorMessage("Members contains 1 duplicate adjunct(s): Id:adjunct-1,Asset:1/1/asset-a");
    }

    [Fact]
    public void Members_SameModelId_DifferentAsset_Valid()
    {
        var sut = GetSut();
        var model = new HydraCollection<Adjunct>
        {
            Members =
            [
                ValidAdjunct("adjunct-1", "1/1/asset-a"),
                ValidAdjunct("adjunct-1", "1/1/asset-b"),
            ]
        };
        var result = sut.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(r => r.Members);
    }

    [Fact]
    public void Members_SameAsset_DifferentModelId_Valid()
    {
        var sut = GetSut();
        var model = new HydraCollection<Adjunct>
        {
            Members =
            [
                ValidAdjunct("adjunct-1", "1/1/asset-a"),
                ValidAdjunct("adjunct-2", "1/1/asset-a"),
            ]
        };
        var result = sut.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(r => r.Members);
    }

    [Fact]
    public void Member_MissingModelId_FailsViaAdjunctValidator()
    {
        var sut = GetSut();
        var model = new HydraCollection<Adjunct>
        {
            Members = [ValidAdjunct(modelId: null!)]
        };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].ModelId");
    }

    [Fact]
    public void Member_MissingMediaType_FailsViaAdjunctValidator()
    {
        var sut = GetSut();
        var adjunct = ValidAdjunct();
        adjunct.MediaType = null;
        var model = new HydraCollection<Adjunct> { Members = [adjunct] };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].MediaType");
    }

    [Fact]
    public void Member_MissingIIIFLink_FailsViaAdjunctValidator()
    {
        var sut = GetSut();
        var adjunct = ValidAdjunct();
        adjunct.IIIFLink = null;
        var model = new HydraCollection<Adjunct> { Members = [adjunct] };
        var result = sut.TestValidate(model);
        result.ShouldHaveValidationErrorFor("Members[0].IIIFLink");
    }

    [Fact]
    public void ValidCollection_NoErrors()
    {
        var sut = GetSut();
        var model = new HydraCollection<Adjunct>
        {
            Members =
            [
                ValidAdjunct("adjunct-1", "1/1/asset-a"),
                ValidAdjunct("adjunct-2", "1/1/asset-a"),
                ValidAdjunct("adjunct-1", "1/1/asset-b"),
            ]
        };
        var result = sut.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
