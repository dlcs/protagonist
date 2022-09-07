using API.Infrastructure.Models;
using DLCS.Model.Assets;

namespace API.Tests.Infrastructure;

public class EntityXTests
{
    [Fact]
    public void SetFieldsForIngestion_ClearsFields()
    {
        // Arrange
        var asset = new Asset { Error = "I am an error", Ingesting = false };
        var expected = new Asset { Error = string.Empty, Ingesting = true };

        // Act
        asset.SetFieldsForIngestion();
        
        // Assert
        asset.Should().BeEquivalentTo(expected);
    }
}