using System;
using System.Collections.Generic;
using API.Features.OriginStrategies.Validators;
using DLCS.HydraModel;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Configuration;

namespace API.Tests.Features.OriginStrategies.Validation;

public class HydraCustomerOriginStrategyValidatorTests
{
    private readonly HydraCustomerOriginStrategyValidator sut;
    
    public HydraCustomerOriginStrategyValidatorTests()
    {
        sut = new HydraCustomerOriginStrategyValidator(GetConfiguration());
    }

    private static IConfiguration GetConfiguration(bool rejectBacktrackingPatterns = true)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
                { ["OriginStrategyRegex:RejectBacktrackingPatterns"] = rejectBacktrackingPatterns.ToString() })
            .Build();
    
    [Fact]
    public void NewCustomerOriginStrategy_CannotHave_AssetId()
    {
        var strategy = new CustomerOriginStrategy()
        {
            Id = Guid.NewGuid().ToString()
        };
        var result = sut.TestValidate(strategy, s => s.IncludeRuleSets("default", "create"));
        result.ShouldHaveValidationErrorFor(s => s.Id);
    }
    
    [Fact]
    public void NewCustomerOriginStrategy_CannotHave_CustomerId()
    {
        var strategy = new CustomerOriginStrategy()
        {
            CustomerId = 1
        };
        var result = sut.TestValidate(strategy, s => s.IncludeRuleSets("default", "create"));
        result.ShouldHaveValidationErrorFor(s => s.CustomerId);
    }
    
    [Fact]
    public void NewCustomerOriginStrategy_Requires_OriginStrategy()
    {
        var strategy = new CustomerOriginStrategy()
        {
            OriginStrategy = null
        };
        var result = sut.TestValidate(strategy, s => s.IncludeRuleSets("default", "create"));
        result.ShouldHaveValidationErrorFor(s => s.OriginStrategy);
    }
    
    [Fact]
    public void NewCustomerOriginStrategy_OriginStrategy_MustBeValid()
    {
        var strategy = new CustomerOriginStrategy()
        {
            OriginStrategy = "basic-http-authentication"
        };
        var result = sut.TestValidate(strategy, s => s.IncludeRuleSets("default", "create"));
        result.ShouldNotHaveValidationErrorFor(s => s.OriginStrategy);
    }
    
    [Fact]
    public void NewCustomerOriginStrategy_Optimised_RequiresS3AmbientStrategy()
    {
        var strategy = new CustomerOriginStrategy()
        {
            OriginStrategy = "basic-http-authentication",
            Optimised = true
        };
        var result = sut.TestValidate(strategy, s => s.IncludeRuleSets("default", "create"));
        result.ShouldHaveValidationErrorFor(s => s.Optimised);
    }

    [Theory]
    [InlineData("basic-http-authentication")]
    [InlineData("sftp")]
    public void NewCustomerOriginStrategy_Requires_Credentials(string originStrategy)
    {
        var strategy = new CustomerOriginStrategy()
        {
            OriginStrategy = originStrategy,
            Regex = "someRegex"
        };
        var result = sut.TestValidate(strategy, s => s.IncludeRuleSets("default", "create"));
        result.ShouldHaveValidationErrorFor(s => s.Credentials);
    }

    [Theory]
    [InlineData("basic-http-authentication")]
    [InlineData("sftp")]
    public void NewCustomerOriginStrategy_WithCredentials_Valid(string originStrategy)
    {
        var strategy = new CustomerOriginStrategy()
        {
            OriginStrategy = originStrategy,
            Regex = "someRegex",
            Credentials = @"{""user"": ""u"", ""password"": ""p""}"
        };
        var result = sut.TestValidate(strategy, s => s.IncludeRuleSets("default", "create"));
        result.ShouldNotHaveValidationErrorFor(s => s.Credentials);
    }

    [Fact]
    public void NewCustomerOriginStrategy_Credentials_NotAllowedForS3Ambient()
    {
        var strategy = new CustomerOriginStrategy()
        {
            OriginStrategy = "s3-ambient",
            Regex = "someRegex",
            Credentials = @"{""user"": ""u"", ""password"": ""p""}"
        };
        var result = sut.TestValidate(strategy, s => s.IncludeRuleSets("default", "create"));
        result.ShouldHaveValidationErrorFor(s => s.Credentials);
    }

    [Fact]
    public void CustomerOriginStrategy_Regex_MustBeValidRegularExpression()
    {
        var strategy = new CustomerOriginStrategy
        {
            Regex = "http[s?://(.*).example.com"
        };
        var result = sut.TestValidate(strategy, s => s.IncludeRuleSets("default"));
        result.ShouldHaveValidationErrorFor(s => s.Regex);
    }

    [Fact]
    public void CustomerOriginStrategy_Regex_ValidRegularExpression_IsAllowed()
    {
        var strategy = new CustomerOriginStrategy
        {
            Regex = "http[s]?://(.*).example.com"
        };
        var result = sut.TestValidate(strategy, s => s.IncludeRuleSets("default"));
        result.ShouldNotHaveValidationErrorFor(s => s.Regex);
    }

    [Theory]
    [InlineData("http[s]?://(?=secure).example.com", "lookahead")]
    [InlineData("http[s]?://(?!secure).example.com", "negative lookahead")]
    [InlineData("(?<=https)://(.*).example.com", "lookbehind")]
    [InlineData("(https?)://(.*).\\1.com", "backreference")]
    [InlineData("(?>http[s]?)://(.*).example.com", "atomic group")]
    public void CustomerOriginStrategy_Regex_RejectsConstructsRequiringBacktracking(string regex, string construct)
    {
        var strategy = new CustomerOriginStrategy
        {
            Regex = regex
        };
        var result = sut.TestValidate(strategy, s => s.IncludeRuleSets("default"));
        result.ShouldHaveValidationErrorFor(s => s.Regex).WithErrorMessage(
            "Regex uses lookarounds, backreferences, atomic groups or conditionals. These can't be evaluated " +
            "in guaranteed linear time so are not supported - rewrite the expression without them");
    }

    [Fact]
    public void CustomerOriginStrategy_Regex_AllowsConstructsRequiringBacktracking_IfRejectionDisabled()
    {
        var validator = new HydraCustomerOriginStrategyValidator(
            GetConfiguration(rejectBacktrackingPatterns: false));
        var strategy = new CustomerOriginStrategy
        {
            // Excluding file extensions needs a negative lookbehind, which has no NonBacktracking-safe rewrite
            Regex = "https://example.com/bucket/.*(?<!\\.tif|\\.jpg)$"
        };
        var result = validator.TestValidate(strategy, s => s.IncludeRuleSets("default"));
        result.ShouldNotHaveValidationErrorFor(s => s.Regex);
    }

    [Fact]
    public void CustomerOriginStrategy_Regex_StillRejectsInvalidRegex_IfRejectionDisabled()
    {
        var validator = new HydraCustomerOriginStrategyValidator(
            GetConfiguration(rejectBacktrackingPatterns: false));
        var strategy = new CustomerOriginStrategy
        {
            Regex = "http[s?://"
        };
        var result = validator.TestValidate(strategy, s => s.IncludeRuleSets("default"));
        result.ShouldHaveValidationErrorFor(s => s.Regex);
    }

    [Fact]
    public void CustomerOriginStrategy_Regex_CannotExceedMaxLength()
    {
        var strategy = new CustomerOriginStrategy
        {
            Regex = new string('a', 1001)
        };
        var result = sut.TestValidate(strategy, s => s.IncludeRuleSets("default"));
        result.ShouldHaveValidationErrorFor(s => s.Regex);
    }
}
