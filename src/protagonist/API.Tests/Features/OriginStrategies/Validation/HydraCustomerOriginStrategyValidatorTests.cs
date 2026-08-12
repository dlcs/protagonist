using System;
using API.Features.OriginStrategies.Validators;
using DLCS.HydraModel;
using FluentValidation.TestHelper;

namespace API.Tests.Features.OriginStrategies.Validation;

public class HydraCustomerOriginStrategyValidatorTests
{
    private readonly HydraCustomerOriginStrategyValidator sut;
    
    public HydraCustomerOriginStrategyValidatorTests()
    {
        sut = new HydraCustomerOriginStrategyValidator();
    }
    
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
}