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

    public IngestControllerTests()
    {
        ingester =  A.Fake<IAssetIngester>();
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

        sut = new IngestController(ingester, Options.Create(engineSettings));
    }

    [Fact]
    public void ReturnAllowedAvOptions_ReturnsAvOptions_WhenCalled()
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
    public void ReturnAllowedAvOptions_ReturnsEmptyList_WhenCalledWithDefaultSettings()
    {
        // Arrange and
        var engineSettings = new EngineSettings()
        {
            TimebasedIngest = new TimebasedIngestSettings()
        };
        
        var ingestController = new IngestController(ingester, Options.Create(engineSettings));
        
        // Act
        var avReturn = ingestController.GetAllowedAvOptions();
        
        var options = avReturn as OkObjectResult;
        var avOptions = options.Value as List<string>;

        // Assert
        options.StatusCode.Should().Be(200);
        avOptions.Count.Should().Be(0);
    }
}