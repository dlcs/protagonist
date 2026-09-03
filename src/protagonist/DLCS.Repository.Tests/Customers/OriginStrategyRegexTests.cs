using System;
using System.Text.RegularExpressions;
using DLCS.Repository.Customers;
using DLCS.Repository.OriginStrategies;

namespace DLCS.Repository.Tests.Customers;

public class OriginStrategyRegexTests
{
    private static readonly OriginStrategySettings DefaultSettings = new();

    [Fact]
    public void Create_UsesNonBacktracking_ForSupportedPattern()
    {
        var regex = OriginStrategyRegex.Create("http[s]?://(.*).example.com", DefaultSettings, out var nonBacktracking);

        nonBacktracking.Should().BeTrue();
        regex.Options.Should().HaveFlag(RegexOptions.NonBacktracking);
        regex.IsMatch("https://foo.example.com").Should().BeTrue();
    }

    [Fact]
    public void Create_IsCaseInsensitive()
    {
        var regex = OriginStrategyRegex.Create("http[s]?://(.*).example.com", DefaultSettings, out _);

        regex.IsMatch("HTTPS://FOO.EXAMPLE.COM").Should().BeTrue();
    }

    [Theory]
    [InlineData("http[s]?://(?=secure).example.com")]
    [InlineData("(https?)://(.*).\\1.com")]
    [InlineData("(?>http[s]?)://(.*).example.com")]
    [InlineData("https://example.com/bucket/.*(?<!\\.tif|\\.jpg)$")]
    public void Create_FallsBackToBacktrackingWithTimeout_ForUnsupportedPattern(string pattern)
    {
        var regex = OriginStrategyRegex.Create(pattern, DefaultSettings, out var nonBacktracking);

        nonBacktracking.Should().BeFalse();
        regex.Options.Should().NotHaveFlag(RegexOptions.NonBacktracking);
        regex.MatchTimeout.Should().Be(DefaultSettings.MatchTimeout);
    }

    [Fact]
    public void Create_DoesNotUseNonBacktracking_IfDisabled()
    {
        var settings = new OriginStrategySettings { UseNonBacktracking = false };

        var regex = OriginStrategyRegex.Create("http[s]?://(.*).example.com", settings, out var nonBacktracking);

        nonBacktracking.Should().BeFalse();
        regex.Options.Should().NotHaveFlag(RegexOptions.NonBacktracking);
        regex.MatchTimeout.Should().Be(settings.MatchTimeout);
    }

    [Fact]
    public void Create_Throws_IfPatternInvalid()
    {
        Action action = () => OriginStrategyRegex.Create("http[s?://", DefaultSettings, out _);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_MatchesCatastrophicPatternWithoutBacktracking()
    {
        // Would take exponential time with the backtracking engine
        var regex = OriginStrategyRegex.Create("^(a+)+$", DefaultSettings, out var nonBacktracking);

        nonBacktracking.Should().BeTrue();
        regex.IsMatch(new string('a', 60) + "!").Should().BeFalse();
    }

    [Theory]
    [InlineData("http[s]?://(.*).example.com")]
    [InlineData("^(a+)+$")]
    public void IsValidPattern_True_ForValidPattern(string pattern)
    {
        OriginStrategyRegex.IsValidPattern(pattern, out var error).Should().BeTrue();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("http[s?://")]
    [InlineData("(unclosed")]
    [InlineData("*")]
    public void IsValidPattern_False_ForInvalidPattern(string pattern)
    {
        OriginStrategyRegex.IsValidPattern(pattern, out var error).Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("http[s]?://(.*).example.com")]
    [InlineData("^(a+)+$")]
    public void SupportsNonBacktracking_True_ForSupportedPattern(string pattern)
        => OriginStrategyRegex.SupportsNonBacktracking(pattern).Should().BeTrue();

    [Theory]
    [InlineData("http[s]?://(?=secure).example.com")]
    [InlineData("http[s]?://(?!secure).example.com")]
    [InlineData("(?<=https)://(.*).example.com")]
    [InlineData("(https?)://(.*).\\1.com")]
    [InlineData("(?>http[s]?)://(.*).example.com")]
    [InlineData("http[s?://")]
    public void SupportsNonBacktracking_False_ForUnsupportedOrInvalidPattern(string pattern)
        => OriginStrategyRegex.SupportsNonBacktracking(pattern).Should().BeFalse();
}
