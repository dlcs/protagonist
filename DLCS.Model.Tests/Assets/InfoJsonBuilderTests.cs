using System.Collections.Generic;
using DLCS.Model.Assets;
using FluentAssertions;
using Xunit;

namespace DLCS.Model.Tests.Assets
{
    public class InfoJsonBuilderTests
    {
        [Fact]
        public void GetImageApi2_1Level0_ReturnsExpected()
        {
            // Arrange
            var expected = @"{
""@context"":""http://iiif.io/api/image/2/context.json"",
""@id"":""https://test.example.com/iiif-img/2/1/jackal"",
""protocol"": ""http://iiif.io/api/image"",
""profile"": [
  ""http://iiif.io/api/image/2/level0.json"",
  {
    ""formats"": [ ""jpg"" ],
    ""qualities"": [ ""color"" ],
    ""supports"": [ ""sizeByWhListed"" ]
  }
  ],
  ""width"": 400,
  ""height"": 800,
  ""sizes"": [
    { ""width"": 100, ""height"": 200 }, { ""width"": 400, ""height"": 800 }
  ]
}
";
            // Act
            var actual = InfoJsonBuilder.GetImageApi2_1Level0(
                "https://test.example.com/iiif-img/2/1/jackal",
                new List<int[]> { new[] { 400, 800 }, new[] { 100, 200 } });
            
            // Assert
            actual.Should().BeEquivalentTo(expected);
        }
        
        [Fact]
        public void GetImageApi2_1Level1_ReturnsExpected()
        {
            // Arrange
            var expected = @"{
""@context"":""http://iiif.io/api/image/2/context.json"",
""@id"":""https://test.example.com/iiif-img/2/1/jackal"",
""protocol"": ""http://iiif.io/api/image"",
""profile"": [
  ""http://iiif.io/api/image/2/level1.json"",
  {
    ""formats"": [ ""jpg"" ],
    ""qualities"": [ ""native"",""color"",""gray"" ],
    ""supports"": [ ""regionByPct"",""sizeByForcedWh"",""sizeByWh"",""sizeAboveFull"",""rotationBy90s"",""mirroring"",""gray"" ]
  }
  ],
  ""width"": 4200,
  ""height"": 8400,
  ""tiles"": [
    { ""width"": 256, ""height"": 256, ""scaleFactors"": [ 1, 2, 4, 8, 16, 32, 64 ] }
  ],
  ""sizes"": [
    { ""width"": 100, ""height"": 200 }, { ""width"": 400, ""height"": 800 }, { ""width"": 1000, ""height"": 2000 }
  ]
}
";
            // Act
            var actual = InfoJsonBuilder.GetImageApi2_1Level1(
                "https://test.example.com/iiif-img/2/1/jackal",
                4200, 8400,
                new List<int[]> {  new[] { 1000, 2000 }, new[] { 400, 800 }, new[] { 100, 200 } });
            
            // Assert
            actual.Should().BeEquivalentTo(expected);
        }
    }
}