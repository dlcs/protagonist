using System;
using System.Collections.Generic;
using DLCS.Core.Settings;

namespace DLCS.Core.Tests.Settings;

public class IngestDefaultSettingsTests
{
    private readonly IngestDefaultSettings settings;

    public IngestDefaultSettingsTests()
    {
        settings = new IngestDefaultSettings
        {
            FamilyDefaults = new Dictionary<string, IngestFamilyDefaults>
            {
                ["F"] = new()
                {
                    DeliveryChannel = "file",
                    OptimisationPolicy = new Dictionary<string, string> { ["*"] = "none" }
                },
                ["T"] = new()
                {
                    DeliveryChannel = "iiif-av",
                    OptimisationPolicy = new Dictionary<string, string>
                        { ["audio"] = "audio-max", ["video"] = "video-max" },
                    ThumbnailPolicy = new Dictionary<string, string>
                        { ["video"] = "video-default" },
                },
                ["I"] = new()
                {
                    DeliveryChannel = "iiif-img",
                    OptimisationPolicy = new Dictionary<string, string> { ["*"] = "default" },
                    ThumbnailPolicy = new Dictionary<string, string> { ["*"] = "image-default" }
                }
            }
        };
    }

    [Fact]
    public void GetPresets_Throws_IfFamilyUnknown()
    {
        Action action = () => settings.GetPresets('X', "whatever");

        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData('I', "image/tiff", "iiif-img", "image-default", "default")]
    [InlineData('F', "application/pdf", "file", null, "none")]
    [InlineData('T', "video/mp4", "iiif-av", "video-default", "video-max")]
    [InlineData('T', "audio/mp4", "iiif-av", null, "audio-max")]
    [InlineData('T', "", "iiif-av", null, null)]
    public void GetPresets_Correct(char family, string mediaType, string channel, string thumb, string iop)
    {
        var result = settings.GetPresets(family, mediaType);

        result.DeliveryChannel.Should().Be(channel);
        result.OptimisationPolicy.Should().Be(iop);
        result.ThumbnailPolicy.Should().Be(thumb);
    }
}