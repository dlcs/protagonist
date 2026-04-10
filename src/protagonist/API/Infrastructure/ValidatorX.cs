using FluentValidation;
using FluentValidation.Results;

namespace API.Infrastructure;

public static class ValidatorX
{
    public static bool PreValidate<T>(this ValidationContext<T?> context, ValidationResult result) where T : class
    {
        if (context.InstanceToValidate != null)
        {
            return true;
        }

        result.Errors.Add(new ValidationFailure("", "Members cannot be null"));
        return false;
    }
}
