using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.ElasticTranscoder.Models;
using Engine.Ingest;
using Engine.Settings;
using FakeItEasy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Engine.Tests.Ingest;

public class IngestControllerTests
{
    private IngestController sut;
    private IAssetIngester ingester;
    private IElasticTranscoderWrapper elasticTranscoderWrapper;

    public IngestControllerTests()
    {
        ingester = A.Fake<IAssetIngester>();
        elasticTranscoderWrapper = A.Fake<IElasticTranscoderWrapper>();
        var engineSettings = new EngineSettings
        {
            TimebasedIngest = new TimebasedIngestSettings()
            {
                DeliveryChannelMappings = new Dictionary<string, string>()
                {
                    {"somePolicy", "An amazon policy"},
                    {"somePolicy2", "An amazon policy 2"}
                }
            }
        };

        A.CallTo(() => elasticTranscoderWrapper.GetPresetIdLookup(A<CancellationToken>._)).Returns(
            new Dictionary<string, TranscoderPreset>()
            {
                { "An amazon policy", new TranscoderPreset("some-id", "An amazon policy", ".ext") },
                { "An amazon policy 2", new TranscoderPreset("some-id-2", "An amazon policy 2", ".ext2") }
            });

        sut = new IngestController(ingester, elasticTranscoderWrapper, Options.Create(engineSettings));
    }

    [Fact]
    public void GetAllowedAvOptions_ReturnsAvOptions_WhenCalled()
    {
        // Arrange and Act
        var avReturn = sut.GetAllowedAvOptions();
        
        var options = avReturn as OkObjectResult;
        var avOptions = options.Value as List<string>;

        // Assert
        options.StatusCode.Should().Be(200);
        avOptions.Count.Should().Be(2);
        avOptions.Should().Contain("somePolicy");
        avOptions.Should().Contain("somePolicy2");
    }
    
    [Fact]
    public void GetAllowedAvOptions_ReturnsEmptyList_WhenCalledWithDefaultSettings()
    {
        // Arrange and
        var engineSettings = new EngineSettings()
        {
            TimebasedIngest = new TimebasedIngestSettings()
        };
        
        var ingestController = new IngestController(ingester, elasticTranscoderWrapper, Options.Create(engineSettings));
        
        // Act
        var avReturn = ingestController.GetAllowedAvOptions();
        
        var options = avReturn as OkObjectResult;
        var avOptions = options.Value as List<string>;

        // Assert
        options.StatusCode.Should().Be(200);
        avOptions.Count.Should().Be(0);
    }
    
    [Fact]
    public async Task GetAllowedAvPresetOptions_ReturnsAvOptions_WhenCalled()
    {
        // Arrange and Act
        var avReturn = await sut.GetAllowedAvPresetOptions();
        
        var options = avReturn as OkObjectResult;
        var avOptions = options.Value as Dictionary<string, TranscoderPreset>;

        // Assert
        options.StatusCode.Should().Be(200);
        avOptions.Count.Should().Be(2);
        avOptions.Keys.Should().Contain("somePolicy");
        avOptions.Keys.Should().Contain("somePolicy");
        avOptions.Values.Any(x => x.Name == "An amazon policy").Should().BeTrue();
        avOptions.Values.Any(x => x.Name == "An amazon policy 2").Should().BeTrue();
    }
    
    [Fact]
    public async Task GetAllowedAvPresetOptions_ReturnsEmptyList_WhenCalledWithDefaultSettings()
    {
        // Arrange and
        var engineSettings = new EngineSettings()
        {
            TimebasedIngest = new TimebasedIngestSettings()
        };
        
        var ingestController = new IngestController(ingester, elasticTranscoderWrapper, Options.Create(engineSettings));
        
        // Act
        var avReturn = await ingestController.GetAllowedAvPresetOptions();
        
        var options = avReturn as OkObjectResult;
        var avOptions = options.Value as Dictionary<string, TranscoderPreset>;

        // Assert
        options.StatusCode.Should().Be(200);
        avOptions.Count.Should().Be(0);
    }
}