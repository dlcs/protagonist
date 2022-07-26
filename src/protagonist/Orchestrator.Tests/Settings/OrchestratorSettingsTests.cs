using System.Collections.Generic;
using DLCS.Core.Types;
using FluentAssertions;
using IIIF.ImageApi;
using Orchestrator.Settings;
using Xunit;

namespace Orchestrator.Tests.Settings;

public class OrchestratorSettingsTests
{
    private readonly OrchestratorSettings imageServerSettings;

    public OrchestratorSettingsTests()
    {
        imageServerSettings = new OrchestratorSettings
        {
            ImageServerPathConfig = new Dictionary<ImageServer, ImageServerConfig>
            {
                [ImageServer.Cantaloupe] = new ImageServerConfig
                {
                    Separator = "%2F",
                    PathTemplate = "%2F{customer}%2F{space}%2F{image-dir}%2F{image}",
                    VersionPathTemplates = new Dictionary<Version, string>
                    {
                        [Version.V2] = "/iiif/2/",
                        [Version.V3] = "/iiif/3/",
                    }
                },
                [ImageServer.IIPImage] = new ImageServerConfig
                {
                    Separator = "/",
                    PathTemplate = "/nas/{customer}/{space}/{image-dir}/{image}.jp2",
                    VersionPathTemplates = new Dictionary<Version, string>
                    {
                        [Version.V2] = "/fcgi-bin/iipsrv.fcgi?IIIF=",
                    }
                }
            }
        };
    }

    [Theory]
    [InlineData(ImageServer.Cantaloupe, "/iiif/3/%2F99%2F100%2Fte%2Fst%2F-i%2Fma%2Ftest-image-name%2Ftest-image-name")]
    [InlineData(ImageServer.IIPImage, "/fcgi-bin/iipsrv.fcgi?IIIF=/nas/99/100/te/st/-i/ma/test-image-name/test-image-name.jp2")]
    public void GetImageServerPath_Correct_ReturnsHighestMatching(ImageServer imageServer, string expected)
    {
        // Arrange
        var asset = new AssetId(99, 100, "test-image-name");
        imageServerSettings.ImageServer = imageServer;
        
        // Act
        var actual = imageServerSettings.GetImageServerPath(asset);

        // Assert
        actual.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(ImageServer.Cantaloupe, Version.V3, "/iiif/3/%2F99%2F100%2Fte%2Fst%2F-i%2Fma%2Ftest-image-name%2Ftest-image-name")]
    [InlineData(ImageServer.Cantaloupe, Version.V2, "/iiif/2/%2F99%2F100%2Fte%2Fst%2F-i%2Fma%2Ftest-image-name%2Ftest-image-name")]
    [InlineData(ImageServer.IIPImage, Version.V2, "/fcgi-bin/iipsrv.fcgi?IIIF=/nas/99/100/te/st/-i/ma/test-image-name/test-image-name.jp2")]
    public void GetImageServerPath_Versioned_Correct(ImageServer imageServer, Version version, string expected)
    {
        // Arrange
        var asset = new AssetId(99, 100, "test-image-name");
        imageServerSettings.ImageServer = imageServer;
        
        // Act
        var actual = imageServerSettings.GetImageServerPath(asset, version);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public void GetImageServerPath_Null_IfCannotMatch()
    {
        // Arrange
        var asset = new AssetId(99, 100, "test-image-name");
        imageServerSettings.ImageServer = ImageServer.IIPImage;
        
        // Act
        var actual = imageServerSettings.GetImageServerPath(asset, Version.V3);

        // Assert
        actual.Should().BeNull();
    }
}