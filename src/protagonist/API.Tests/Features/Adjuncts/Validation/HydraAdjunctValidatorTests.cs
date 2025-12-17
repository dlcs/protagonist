using API.Features.Adjuncts.Validation;
using DLCS.HydraModel;
using FluentValidation.TestHelper;

namespace API.Tests.Features.Adjuncts.Validation;

public class HydraAdjunctValidatorTests
{
    private readonly HydraAdjunctValidator sut = new();
    
    [Fact]
    public void Valid_WhenAllValidatorsUsed()
    {
        var adjunct = new Adjunct("https://localhost", 1, 1, "assetId", "adjunctId")
        {
            ExternalId = "https://localhost:2000/some-id",
            MediaType = "mediaType",
            IIIFLink = "SeeAlso",
            Type = "Image",
            Id = "https://localhost/customers/1/spaces/1/images/assetId/adjuncts/adjunctId",
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
            IIIFLink = "SeeAlso",
            Type = "Image"
        };
        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(r => r.ExternalId)
            .WithErrorMessage("'externalId' must be a well formed URI");
    }
    
    [Fact]
    public void IiifLink_NotValid()
    {
        var adjunct = new Adjunct("https://localhost", 1, 1, "assetId", "adjunctId")
        {
            MediaType = "mediaType",
            IIIFLink = "Invalid",
            Type = "Image"
        };
        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(r => r.IIIFLink)
            .WithErrorMessage("Valid values for 'iiifLink' are 'SeeAlso', 'Annotations' and 'Rendering'");
    }
    
    [Fact]
    public void Id_NotMatched()
    {
        var adjunct = new Adjunct("https://localhost", 1, 1, "assetId", "adjunctId")
        {
            MediaType = "mediaType",
            IIIFLink = "SeeAlso",
            Type = "Image",
            Id = "https://localhost/customers/1/spaces/1/images/assetId/adjuncts/different",
        };
        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(r => r)
            .WithErrorMessage("'id' and '@id' must have a matching adjunct identifier");
    }
    
    [Fact]
    public void IiifLink_Null()
    {
        var adjunct = new Adjunct("https://localhost", 1, 1, "assetId", "adjunctId")
        {
            MediaType = "mediaType",
            Type = "Image"
        };
        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(r => r.IIIFLink)
            .WithErrorMessage("'iiifLink' is required");
    }
    
    [Fact]
    public void MediaType_Null()
    {
        var adjunct = new Adjunct("https://localhost", 1, 1, "assetId", "adjunctId")
        {
            IIIFLink = "SeeAlso",
            Type = "Image"
        };
        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(r => r.MediaType)
            .WithErrorMessage("'mediaType' is required");
    }
    
    [Fact]
    public void ModelId_Null_WhenCreate()
    {
        var adjunct = new Adjunct("https://localhost", 1, 1, "assetId", null)
        {
            MediaType = "mediaType",
            IIIFLink = "SeeAlso",
            Type = "Image"
        };
        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(r => r.ModelId)
            .WithErrorMessage("Adjunct identifier could not be found");
    }
    
    [Fact]
    public void Type_Error_WhenNotSet()
    {
        var adjunct = new Adjunct("https://localhost", 1, 1, "assetId", null)
        {
            MediaType = "mediaType",
            IIIFLink = "SeeAlso"
        };
        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(r => r.Type)
            .WithErrorMessage("'@type' must be one of the following: Image, Sound, Video, Text, model, Dataset");
    }
    
    [Fact]
    public void Type_Error_WhenIncorrect()
    {
        var adjunct = new Adjunct("https://localhost", 1, 1, "assetId", null)
        {
            MediaType = "mediaType",
            IIIFLink = "SeeAlso",
            Type = "wrong"
        };
        var result = sut.TestValidate(adjunct);
        result.ShouldHaveValidationErrorFor(r => r.Type)
            .WithErrorMessage("'@type' must be one of the following: Image, Sound, Video, Text, model, Dataset");
    }
}
