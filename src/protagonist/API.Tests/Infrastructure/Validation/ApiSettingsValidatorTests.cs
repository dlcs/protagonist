using API.Settings;
using API.Settings.Validation;
using FluentValidation.TestHelper;

namespace API.Tests.Infrastructure.Validation;

public class ApiSettingsValidatorTests
{
    private readonly ApiSettingsValidator sut = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Salt_Null(string salt)
    {
        var settings = new ApiSettings { ApiSalt = salt };
        var result = sut.TestValidate(settings);
        result.ShouldHaveValidationErrorFor(a => a.ApiSalt);
    }
}