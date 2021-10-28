using FluentAssertions;
using IIIF.Presentation;
using Orchestrator.Settings;
using Xunit;

namespace Orchestrator.Tests.Settings
{
    public class OrchestratorSettingsTests
    {
        [Theory]
        [InlineData("2.1", Version.V2)]
        [InlineData("3.0", Version.V3)]
        [InlineData("something else", Version.V3)]
        public void GetDefaultIIIFPresentationVersion_Correct(string value, Version expected)
        {
            // Arrange
            var settings = new OrchestratorSettings { DefaultIIIFPresentationVersion = value };
            
            // Act
            var actual = settings.GetDefaultIIIFPresentationVersion();
            
            // Assert
            actual.Should().Be(expected);
        }
    }
}