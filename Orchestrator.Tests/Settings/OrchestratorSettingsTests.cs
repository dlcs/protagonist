using FluentAssertions;
using Orchestrator.Settings;
using Xunit;

namespace Orchestrator.Tests.Settings
{
    public class OrchestratorSettingsTests
    {
        [Theory]
        [InlineData("2.1", IIIF.Presentation.Version.V2)]
        [InlineData("3.0", IIIF.Presentation.Version.V3)]
        [InlineData("something else", IIIF.Presentation.Version.V3)]
        public void GetDefaultIIIFPresentationVersion_Correct(string value, IIIF.Presentation.Version expected)
        {
            // Arrange
            var settings = new OrchestratorSettings { DefaultIIIFPresentationVersion = value };
            
            // Act
            var actual = settings.GetDefaultIIIFPresentationVersion();
            
            // Assert
            actual.Should().Be(expected);
        }
        
        [Theory]
        [InlineData("2.1", IIIF.ImageApi.Version.V2)]
        [InlineData("3.0", IIIF.ImageApi.Version.V3)]
        [InlineData("something else", IIIF.ImageApi.Version.V3)]
        public void GetDefaultIIIFImageVersion_Correct(string value, IIIF.ImageApi.Version expected)
        {
            // Arrange
            var settings = new OrchestratorSettings { DefaultIIIFImageVersion = value };
            
            // Act
            var actual = settings.GetDefaultIIIFImageVersion();
            
            // Assert
            actual.Should().Be(expected);
        }
    }
}