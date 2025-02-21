using FluentValidation;

namespace API.Settings.Validation;

public class ApiSettingsValidator : AbstractValidator<ApiSettings>
{
    public ApiSettingsValidator()
    {
        RuleFor(a => a.ApiSalt).NotEmpty();
    }    
}