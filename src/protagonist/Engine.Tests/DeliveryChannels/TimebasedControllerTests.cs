using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models;
using Engine.DeliveryChannels;
using FakeItEasy;

namespace Engine.Tests.DeliveryChannels;

public class TimebasedControllerTests
{
    private readonly ITranscoderPresetLookup transcoderPreset;
    private readonly TimebasedController sut;

    public TimebasedControllerTests()
    {
        transcoderPreset = A.Fake<ITranscoderPresetLookup>();

        sut = new TimebasedController(transcoderPreset);
    }

    [Fact]
    public void GetAllowedAvOptions_ReturnsPolicyDataNames_WhenCalled()
    {
        // Arrange
        A.CallTo(() => transcoderPreset.GetPresetLookupByPolicyName()).Returns(
            new Dictionary<string, TranscoderPreset>
            {
                { "An amazon policy", new TranscoderPreset("some-id", "An amazon policy", ".ext") },
                { "An amazon policy 2", new TranscoderPreset("some-id-2", "An amazon policy 2", ".ext2") }
            });

        var expected = new List<string> { "An amazon policy", "An amazon policy 2" };
        
        // Act
        var avOptions = sut.GetAllowedAvOptions();
        
        // Assert
        avOptions.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void GetKnownAvPresetOptions_ReturnsPolicyDataByName_WhenCalled()
    {
        // Arrange 
        var presetsById = new Dictionary<string, TranscoderPreset>
        {
            { "An amazon policy", new TranscoderPreset("some-id", "An amazon policy", ".ext") },
            { "An amazon policy 2", new TranscoderPreset("some-id-2", "An amazon policy 2", ".ext2") }
        };
        
        A.CallTo(() => transcoderPreset.GetPresetLookupByPolicyName()).Returns(
            presetsById);
        
        // Act
        var avOptions = sut.GetKnownPresets();
        
        // Assert
        avOptions.Should().BeEquivalentTo(presetsById);
    }
}
