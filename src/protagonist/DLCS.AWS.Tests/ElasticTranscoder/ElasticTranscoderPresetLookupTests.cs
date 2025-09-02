using Amazon.ElasticTranscoder;
using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.ElasticTranscoder;
using DLCS.Core.Caching;
using FakeItEasy;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.Tests.ElasticTranscoder;

[Obsolete("Replaced by MediaConvert")]
public class ElasticTranscoderPresetLookupTests
{
    private readonly ElasticTranscoderPresetLookup sut;

    public ElasticTranscoderPresetLookupTests()
    {
        var elasticTranscoder = A.Fake<IAmazonElasticTranscoder>();
        
        A.CallTo(() => elasticTranscoder.ListPresetsAsync(A<ListPresetsRequest>._, A<CancellationToken>._)).Returns(new ListPresetsResponse()
        {
            Presets = new List<Preset>
            {
                new()
                {
                    Id = "1234567890123-abcdef",
                    Name = "some-preset-name"
                },
                new()
                {
                    Id = "9999999999999-zzzzzz",
                    Name = "some-preset-2-name"
                }
            }
        });
        
        var cacheSettings = Options.Create(new CacheSettings());
        sut = new ElasticTranscoderPresetLookup(elasticTranscoder, new MockCachingService(), cacheSettings,
            new NullLogger<ElasticTranscoderPresetLookup>());
    }

    [Fact]
    public async Task GetPresetLookupById_ReturnsPresets_WhenCalled()
    {
        // Act
        var presets = sut.GetPresetLookupById();

        // Assert
        presets.Should().HaveCount(2);
        presets.Should().ContainKeys("1234567890123-abcdef", "9999999999999-zzzzzz");
    }
    
    [Fact]
    public async Task GetPresetLookupByName_ReturnsPresets_WhenCalled()
    {
        // Act
        var presets = sut.GetPresetLookupByPolicyName();

        // Assert
        presets.Should().HaveCount(2);
        presets.Should().ContainKeys("some-preset-name", "some-preset-2-name");
    }
}
