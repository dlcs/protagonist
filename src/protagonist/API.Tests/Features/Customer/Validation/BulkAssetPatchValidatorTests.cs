using API.Features.Image.Validation;
using API.Infrastructure.Requests;
using API.Settings;
using DLCS.Model;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;

namespace API.Tests.Features.Customer.Validation;

public class BulkAssetPatchValidatorTests
{
    private readonly BulkAssetPatchValidator sut;

    public BulkAssetPatchValidatorTests()
    {
        var apiSettings = new ApiSettings { MaxImageListSize = 4 };
        sut = new BulkAssetPatchValidator(Options.Create(apiSettings));
    }
    
    [Fact]
    public void Members_Null()
    {
        BulkPatch<IdentifierOnly> bulkPatch = new BulkPatch<IdentifierOnly>()
        {
            Field = "unsupported",
            Value = [""],
            Members = null
        };;
        var result = sut.TestValidate(bulkPatch);
        result.ShouldHaveValidationErrorFor(r => r.Members)
            .WithErrorMessage("Member cannot be null");;
    }
    
    [Fact]
    public void Field_Unsupported()
    {
        BulkPatch<IdentifierOnly> bulkPatch = new BulkPatch<IdentifierOnly>()
        {
            Field = "unsupported",
            Value = [""],
            Members = []
        };
        var result = sut.TestValidate(bulkPatch);
        result.ShouldHaveValidationErrorFor(r => r.Field)
            .WithErrorMessage("Unsupported field 'unsupported'");;
    }
}
