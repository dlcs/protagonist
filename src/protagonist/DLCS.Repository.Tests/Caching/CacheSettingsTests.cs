using System;
using System.Collections.Generic;
using DLCS.Core.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace DLCS.Repository.Tests.Caching;

public class CacheSettingsTests
{
    private const string KnownOverride = CacheOverrideKeys.Policy;

    private readonly CacheSettings sut;

    public CacheSettingsTests()
    {
        sut = new CacheSettings
        {
            TimeToLive = new Dictionary<CacheSource, CacheGroupSettings>()
        };

        sut.TimeToLive[CacheSource.Memory] = new CacheGroupSettings
        {
            ShortTtlSecs = 10,
            DefaultTtlSecs = 20,
            LongTtlSecs = 30,
            Overrides = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [KnownOverride] = 50
            }
        };
    }

    [Theory]
    [InlineData(CacheDuration.Short, CacheSource.Memory, 10)]
    [InlineData(CacheDuration.Default, CacheSource.Memory, 20)]
    [InlineData(CacheDuration.Long, CacheSource.Memory, 30)]
    public void GetTtl_Default_ReturnsExpected(CacheDuration duration, CacheSource source, int expected)
        => sut.GetTtl(duration, source).Should().Be(expected);

    [Theory]
    [InlineData(CacheDuration.Short, CacheSource.Distributed, 10)]
    [InlineData(CacheDuration.Default, CacheSource.Distributed, 20)]
    [InlineData(CacheDuration.Long, CacheSource.Distributed, 30)]
    public void GetTtl_ReturnsMemoryValue_IfSourceNotFound(CacheDuration duration, CacheSource source, int expected)
        => sut.GetTtl(duration, source).Should().Be(expected);

    [Theory]
    [InlineData(CacheDuration.Short)]
    [InlineData(CacheDuration.Default)]
    [InlineData(CacheDuration.Long)]
    public void GetTtl_Named_OverrideTakesPrecedenceOverDuration(CacheDuration duration)
        => sut.GetTtl(duration, named: KnownOverride).Should().Be(50);

    [Fact]
    public void GetTtl_Named_IsCaseInsensitive()
        => sut.GetTtl(named: KnownOverride.ToUpperInvariant()).Should().Be(50);

    [Theory]
    [InlineData(CacheDuration.Short, 10)]
    [InlineData(CacheDuration.Default, 20)]
    [InlineData(CacheDuration.Long, 30)]
    public void GetTtl_Named_FallsBackToDuration_IfOverrideNotFound(CacheDuration duration, int expected)
        => sut.GetTtl(duration, named: "__notfound__").Should().Be(expected);

    [Fact]
    public void GetTtl_Named_UsesMemoryOverride_IfSourceNotFound()
        => sut.GetTtl(CacheDuration.Long, CacheSource.Distributed, KnownOverride).Should().Be(50);

    [Fact]
    public void GetTtl_Named_DoesNotThrow_IfSourceHasNoOverrides()
    {
        var noOverrides = new CacheSettings
        {
            TimeToLive = new Dictionary<CacheSource, CacheGroupSettings>
            {
                [CacheSource.Memory] = new() { LongTtlSecs = 30 }
            }
        };

        noOverrides.GetTtl(CacheDuration.Long, named: KnownOverride).Should().Be(30);
    }

    [Theory]
    [InlineData(CacheDuration.Short, CacheSource.Memory, 60)]
    [InlineData(CacheDuration.Short, CacheSource.Distributed, 60)]
    [InlineData(CacheDuration.Default, CacheSource.Memory, 600)]
    [InlineData(CacheDuration.Default, CacheSource.Distributed, 600)]
    [InlineData(CacheDuration.Long, CacheSource.Memory, 1800)]
    [InlineData(CacheDuration.Long, CacheSource.Distributed, 1800)]
    public void GetTtl_EmptyInstance_ReturnsFallback(CacheDuration duration, CacheSource source, int expected)
        => new CacheSettings().GetTtl(duration, source).Should().Be(expected);

    [Fact]
    public void GetTtl_Named_EmptyInstance_ReturnsFallback()
        => new CacheSettings().GetTtl(named: KnownOverride).Should().Be(600);

    [Fact]
    public void GetMemoryCacheOptions_UsesDuration_IfNoOverrideNamed()
    {
        var options = sut.GetMemoryCacheOptions(CacheDuration.Long, 5, CacheItemPriority.High);

        options.AbsoluteExpirationRelativeToNow.Should().Be(TimeSpan.FromSeconds(30));
        options.Size.Should().Be(5);
        options.Priority.Should().Be(CacheItemPriority.High);
    }

    [Fact]
    public void GetMemoryCacheOptions_Named_OverrideTakesPrecedenceOverDuration()
    {
        var options = sut.GetMemoryCacheOptions(CacheDuration.Long, 5, CacheItemPriority.High, KnownOverride);

        options.AbsoluteExpirationRelativeToNow.Should().Be(TimeSpan.FromSeconds(50));
        options.Size.Should().Be(5);
        options.Priority.Should().Be(CacheItemPriority.High);
    }

    /// <summary>
    /// Overrides can be provided via envvar, where the casing of the key can't be relied upon. This verifies that the
    /// case-insensitive comparer that CacheGroupSettings.Overrides is initialised with survives config binding.
    /// </summary>
    [Fact]
    public void GetTtl_Named_IsCaseInsensitive_WhenBoundFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Caching:TimeToLive:Memory:LongTtlSecs"] = "30",
                ["Caching:TimeToLive:Memory:Overrides:POLICY"] = "50"
            })
            .Build();

        var bound = config.GetSection("Caching").Get<CacheSettings>()!;

        bound.GetTtl(CacheDuration.Long, named: CacheOverrideKeys.Policy).Should().Be(50);
    }
}
