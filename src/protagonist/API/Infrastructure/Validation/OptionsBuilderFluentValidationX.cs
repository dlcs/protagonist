using System.Collections.Generic;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace API.Infrastructure.Validation;

/// <summary>
/// Extension method to configure FluentValidation of IOptions
/// </summary>
/// <remarks>
/// See https://andrewlock.net/adding-validation-to-strongly-typed-configuration-objects-using-flentvalidation/
/// </remarks>
public static class OptionsBuilderFluentValidationX
{
    public static OptionsBuilder<TOptions> ValidateFluentValidation<TOptions>(
        this OptionsBuilder<TOptions> optionsBuilder) where TOptions : class
    {
        optionsBuilder.Services.AddSingleton<IValidateOptions<TOptions>>(
            provider => new FluentValidationOptions<TOptions>(
                optionsBuilder.Name, provider));
        return optionsBuilder;
    }
}

public class FluentValidationOptions<TOptions> 
    : IValidateOptions<TOptions> where TOptions : class
{
    private readonly IServiceProvider serviceProvider;
    private readonly string? name;
    public FluentValidationOptions(string? name, IServiceProvider serviceProvider)
    {
        // we need the service provider to create a scope later
        this.serviceProvider = serviceProvider; 
        this.name = name; // Handle named options
    }

    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        // Null name is used to configure all named options.
        if (this.name != null && this.name != name)
        {
            // Ignored if not validating this instance.
            return ValidateOptionsResult.Skip;
        }

        // Ensure options are provided to validate against
        ArgumentNullException.ThrowIfNull(options);
        
        // Validators are typically registered as scoped,
        // so we need to create a scope to be safe, as this
        // method is be called from the root scope
        using IServiceScope scope = serviceProvider.CreateScope();

        // retrieve an instance of the validator
        var validator = scope.ServiceProvider.GetRequiredService<IValidator<TOptions>>();

        // Run the validation
        ValidationResult results = validator.Validate(options);
        if (results.IsValid)
        {
            // All good!
            return ValidateOptionsResult.Success;
        }

        // Validation failed, so build the error message
        string typeName = options.GetType().Name;
        var errors = new List<string>();
        foreach (var result in results.Errors)
        {
            errors.Add(
                $"Fluent validation failed for '{typeName}.{result.PropertyName}' with the error: '{result.ErrorMessage}'.");
        }

        return ValidateOptionsResult.Fail(errors);
    }
}
