using DLCS.AWS.Settings;
using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models;
using DLCS.Core.Caching;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Settings;

namespace DLCS.AWS.Tests.Settings;

public class SettingsBasedPresetLookupTests
{
    private static SettingsBasedPresetLookup GetSut(Dictionary<string, string>? transcodes = null)
    {
        var transcodeSettings = new TranscodeSettings
        {
            QueueName = "QueueName",
            RoleArn = "roleArn",
            DeliveryChannelMappings = transcodes ?? new Dictionary<string, string>
            {
                ["audio"] = "System-Preset|mp3",
                ["video"] = "Custom-Preset|mp4"
            }
        };
        return new SettingsBasedPresetLookup(new MockCachingService(),
            OptionsHelpers.GetOptionsMonitor(new AWSSettings
            {
                Transcode = transcodeSettings,
            }),
            OptionsHelpers.GetOptionsMonitor(new CacheSettings()),
            new NullLogger<SettingsBasedPresetLookup>());
    }
    
    [Fact]
    public void GetPresetLookupByName_Throws_IfInvalidFormatPreset()
    {
        var sut = GetSut(new Dictionary<string, string>
        {
            ["audio"] = "invalid-as-no-extension",
        });
        Action action = () => sut.GetPresetLookupByPolicyName();
        action.Should().Throw<InvalidOperationException>();
    }
    
    [Fact]
    public void GetPresetLookupByName_ReturnsExpected()
    {
        var expected = new Dictionary<string, TranscoderPreset>
        {
            ["audio"] = new("System-Preset", "audio", "mp3"),
            ["video"] = new("Custom-Preset", "video", "mp4"),
        };
        GetSut().GetPresetLookupByPolicyName().Should().BeEquivalentTo(expected);
    }
}
