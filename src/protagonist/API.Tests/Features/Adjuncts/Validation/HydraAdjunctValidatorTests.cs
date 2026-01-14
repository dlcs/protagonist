using API.Features.Adjuncts.Validation;
using API.Settings;
using DLCS.HydraModel;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;

namespace API.Tests.Features.Adjuncts.Validation;

public class HydraAdjunctValidatorTests
{
    private readonly HydraAdjunctValidator sut = new(Options.Create(
        new ApiSettings
        {
            RestrictedResourceIdCharacterString = "\\ /"
        }));
    
    [Fact]
    public void Valid_WhenAllValidatorsUsed()
    {
        var adjunct = new Adjunct("https://localhost", 1, 1, "assetId", "adjunctId")
        {
            ExternalId = "https://localhost:2000/some-id",
            MediaType = "mediaType",
            IIIFLink = "seeAlso",
            Type = "Image",
            Language = ["fra", "en"]
        };
        var result = sut.TestValidate(adjunct);
        result.ShouldNotHaveAnyValidationErrors();
    }
    
    [Fact]
    public void ExternalId_NotValidUri()
    {
        var adjunct = new Adjunct("https://localhost", 1, 1, "assetId", "adjunctId")
        {
            ExternalId = "not-uri",
            MediaType = "mediaType",
            IIIFLink = "seeAlso",
            Type = "Image"
        };
        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(r => r.ExternalId)
            .WithErrorMessage("'externalId' is required and must be a well formed URI");
    }
    
    [Fact]
    public void ExternalId_Null()
    {
        var adjunct = new Adjunct
        {
            ExternalId = null,
            MediaType = "mediaType",
            IIIFLink = "seeAlso",
            Type = "Image"
        };
        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(r => r.ExternalId)
            .WithErrorMessage("'externalId' is required and must be a well formed URI");
    }
    
    [Theory]
    [InlineData("SeeAlso")]
    [InlineData("Invalid")]
    public void IIIFLink_NotValid(string iiifLink)
    {
        var adjunct = new Adjunct
        {
            MediaType = "mediaType",
            IIIFLink = iiifLink,
            Type = "Image",
            ExternalId = "https://localhost/customers/1/spaces/1/images/assetId/valid"
        };
        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(r => r.IIIFLink)
            .WithErrorMessage("Valid values for 'iiifLink' are 'seeAlso', 'annotations', 'rendering'");
    }
    
    [Fact]
    public void IIIFLink_Null()
    {
        var adjunct = new Adjunct
        {
            MediaType = "mediaType",
            Type = "Image",
            ExternalId = "https://localhost/customers/1/spaces/1/images/assetId/valid"
        };
        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(r => r.IIIFLink)
            .WithErrorMessage("'iiifLink' is required");
    }
    
    [Fact]
    public void MediaType_Null()
    {
        var adjunct = new Adjunct
        {
            IIIFLink = "seeAlso",
            Type = "Image",
            ExternalId = "https://localhost/customers/1/spaces/1/images/assetId/valid"
        };
        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(r => r.MediaType)
            .WithErrorMessage("'mediaType' is required");
    }
    
    [Fact]
    public void ModelId_Null()
    {
        var adjunct = new Adjunct
        {
            MediaType = "mediaType",
            IIIFLink = "seeAlso",
            Type = "Image",
            ExternalId = "https://localhost/customers/1/spaces/1/images/assetId/valid"
        };
        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(r => r.ModelId)
            .WithErrorMessage("Adjunct identifier could not be found");
    }
    
    [Theory]
    [InlineData(" space")]
    [InlineData("slash\\")]
    [InlineData("other/slash")]
    public void ModelId_InvalidCharacters(string modelId)
    {
        var adjunct = new Adjunct
        {
            MediaType = "mediaType",
            IIIFLink = "seeAlso",
            Type = "Image",
            ExternalId = "https://localhost/customers/1/spaces/1/images/assetId/valid",
            ModelId = modelId
        };
        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(r => r.ModelId)
            .WithErrorMessage("Adjunct id contains at least one of the following restricted characters. Invalid values are: \\ /");
    }
    
    [Fact]
    public void ModelId_TooLong()
    {
        var adjunct = new Adjunct
        {
            MediaType = "mediaType",
            IIIFLink = "seeAlso",
            Type = "Image",
            ExternalId = "https://localhost/customers/1/spaces/1/images/assetId/valid",
            ModelId = new string('a', 201),
        };
        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(r => r.ModelId)
            .WithErrorMessage("Adjunct id must be 200 characters or less");
    }
    
    [Fact]
    public void Type_Error_WhenNotSet()
    {
        var adjunct = new Adjunct
        {
            MediaType = "mediaType",
            IIIFLink = "seeAlso",
            Type = null,
            ExternalId = "https://localhost/customers/1/spaces/1/images/assetId"
        };
        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(r => r.Type)
            .WithErrorMessage("'@type' is required");
    }
    
    [Fact]
    public void Language_Error_WhenLongerThan3Chars()
    {
        var adjunct = new Adjunct
        {
            MediaType = "mediaType",
            IIIFLink = "seeAlso",
            Type = "AnnotationPage",
            Language = ["en", "german"],
            ExternalId = "https://localhost/customers/1/spaces/1/images/assetId"
        };
        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(r => r.Language)
            .WithErrorMessage("All 'language' values must be 3 characters or less");
    }
}
