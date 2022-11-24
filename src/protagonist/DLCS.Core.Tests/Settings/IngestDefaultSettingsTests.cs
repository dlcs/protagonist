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
                    OptimisationPolicy = new Dictionary<string, string> { ["*"] = "none" },
                    ThumbnailPolicy = ""
                },
                ["T"] = new()
                {
                    DeliveryChannel = "iiif-av",
                    OptimisationPolicy = new Dictionary<string, string>
                        { ["audio"] = "audio-max", ["video"] = "video-max" },
                    ThumbnailPolicy = "video-default"
                },
                ["I"] = new()
                {
                    DeliveryChannel = "iiif-img",
                    OptimisationPolicy = new Dictionary<string, string> { ["*"] = "default" },
                    ThumbnailPolicy = "image-default"
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
    [InlineData('F', "application/pdf", "file", "", "none")]
    [InlineData('T', "video/mp4", "iiif-av", "video-default", "video-max")]
    [InlineData('T', "audio/mp4", "iiif-av", "video-default", "audio-max")]
    [InlineData('T', "", "iiif-av", "video-default", null)]
    public void GetPresets_Correct(char family, string mediaType, string channel, string thumb, string iop)
    {
        var result = settings.GetPresets(family, mediaType);

        result.DeliveryChannel.Should().Be(channel);
        result.OptimisationPolicy.Should().Be(iop);
        result.ThumbnailPolicy.Should().Be(thumb);
    }
}