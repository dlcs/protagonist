using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.ElasticTranscoder.Models;
using Engine.DeliveryChannels;
using Engine.Settings;
using FakeItEasy;
using Microsoft.Extensions.Options;

namespace Engine.Tests.DeliveryChannels;

public class TimebasedControllerTests
{
    private readonly IElasticTranscoderPresetLookup elasticTranscoderPreset;

    public TimebasedControllerTests()
    {
        elasticTranscoderPreset = A.Fake<IElasticTranscoderPresetLookup>();
        
        A.CallTo(() => elasticTranscoderPreset.GetPresetLookupByName(A<CancellationToken>._)).Returns(
            new Dictionary<string, TranscoderPreset>()
            {
                { "An amazon policy", new TranscoderPreset("some-id", "An amazon policy", ".ext") },
                { "An amazon policy 2", new TranscoderPreset("some-id-2", "An amazon policy 2", ".ext2") }
            });
    }

    [Fact]
    public void GetAllowedAvOptions_ReturnsAvOptions_WhenCalled()
    {
        // Act
        var avOptions = GetSut().GetAllowedAvOptions();
        
        // Assert
        avOptions.Should().HaveCount(2);
        avOptions.Should().Contain("somePolicy");
        avOptions.Should().Contain("somePolicy2");
    }
    
    [Fact]
    public void GetAllowedAvOptions_ReturnsEmptyList_WhenCalledWithDefaultSettings()
    {
        // Act
        var avOptions = GetSut(new TimebasedIngestSettings()).GetAllowedAvOptions();
        
        // Assert
        avOptions.Should().HaveCount(0);
    }
    
    [Fact]
    public async Task GetKnownAvPresetOptions_ReturnsAvOptions_WhenCalled()
    {
        // Arrange and Act
        var avOptions = await GetSut().GetKnownPresets();
        
        // Assert
        avOptions.Should().HaveCount(2);
        avOptions.Should()
            .ContainKey("somePolicy")
            .WhoseValue.Name.Should().Be("An amazon policy");
        avOptions.Should()
            .ContainKey("somePolicy2")
            .WhoseValue.Name.Should().Be("An amazon policy 2");
    }
    
    [Fact]
    public async Task GetKnownAvPresetOptions_ReturnsEmptyList_WhenCalledWithDefaultSettings()
    {
        // Act
        var avOptions = await GetSut(new TimebasedIngestSettings()).GetKnownPresets();
        
        // Assert
        avOptions.Should().HaveCount(0);
    }
    
    private TimebasedController GetSut(TimebasedIngestSettings? settings = null)
    {
        var engineSettings = new EngineSettings
        {
            TimebasedIngest = settings ?? new TimebasedIngestSettings
            {
                DeliveryChannelMappings = new Dictionary<string, string>
                {
                    { "somePolicy", "An amazon policy" },
                    { "somePolicy2", "An amazon policy 2" }
                }
            }
        };
        
        return new TimebasedController(elasticTranscoderPreset, Options.Create(engineSettings));
    }
}
