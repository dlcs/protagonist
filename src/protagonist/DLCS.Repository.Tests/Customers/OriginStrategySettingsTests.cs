using System;
using System.Collections.Generic;
using DLCS.Repository.OriginStrategies;
using Microsoft.Extensions.Configuration;

namespace DLCS.Repository.Tests.Customers;

public class OriginStrategySettingsTests
{
    [Fact]
    public void FromConfiguration_ReturnsDefaults_IfSectionAbsent()
    {
        var settings = OriginStrategySettings.FromConfiguration(GetConfiguration([]));

        settings.UseNonBacktracking.Should().BeTrue();
        settings.RejectBacktrackingPatterns.Should().BeTrue();
        settings.MatchTimeout.Should().Be(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void FromConfiguration_ReadsConfiguredValues()
    {
        var settings = OriginStrategySettings.FromConfiguration(GetConfiguration(new()
        {
            ["OriginStrategy:UseNonBacktracking"] = "false",
            ["OriginStrategy:RejectBacktrackingPatterns"] = "false",
            ["OriginStrategy:MatchTimeout"] = "00:00:00.250"
        }));

        settings.UseNonBacktracking.Should().BeFalse();
        settings.RejectBacktrackingPatterns.Should().BeFalse();
        settings.MatchTimeout.Should().Be(TimeSpan.FromMilliseconds(250));
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-00:00:01")]
    public void FromConfiguration_Throws_IfMatchTimeoutNotPositive(string matchTimeout)
    {
        Action action = () => OriginStrategySettings.FromConfiguration(GetConfiguration(new()
            { ["OriginStrategy:MatchTimeout"] = matchTimeout }));

        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("not-a-range")]
    [InlineData("10.0.0.0")]
    [InlineData("10.0.0.0/33")]
    [InlineData("2001:4860:4860::8888/1025")]
    public void FromConfiguration_Throws_IfBlockedIpRangeInvalid(string range)
    {
        Action action = () => OriginStrategySettings.FromConfiguration(GetConfiguration(new()
            { ["OriginStrategy:BlockedIpRanges:0"] = range }));

        action.Should().Throw<ArgumentException>().WithMessage($"*{range}*");
    }

    private static IConfiguration GetConfiguration(Dictionary<string, string> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
