using System.Collections.Generic;
using DLCS.Repository.Caching;
using FluentAssertions;
using Xunit;

namespace DLCS.Repository.Tests.Caching;

public class CacheSettingsTests
{
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
            Overrides = new Dictionary<string, int>
            {
                ["override:mem"] = 50
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

    [Fact]
    public void GetTtl_Named_ReturnsExpected()
    {
        var actual = sut.GetTtl("override:mem");
        actual.Should().Be(50);
    }
    
    [Fact]
    public void GetTtl_Named_ReturnsDefault_IfOverrideNotFound()
    {
        var actual = sut.GetTtl("__notfound__");
        actual.Should().Be(20);
    }
    
    [Fact]
    public void GetTtl_Named_ReturnsDefault_IfSourceNotFound()
    {
        var actual = sut.GetTtl("override:mem", CacheSource.Distributed);
        actual.Should().Be(20);
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
        => new CacheSettings().GetTtl("override:mem").Should().Be(600);
}