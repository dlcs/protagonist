using System.Collections.Generic;
using DLCS.Model.Assets;
using FluentAssertions;
using IIIF.Serialisation;
using Xunit;

namespace DLCS.Model.Tests.Assets;

public class InfoJsonBuilderTests
{
[Fact]
public void GetImageApi2_1Level0_ReturnsExpected()
{
  // Arrange
  var expected = @"{
  ""@context"": ""http://iiif.io/api/image/2/context.json"",
  ""@id"": ""https://test.example.com/iiif-img/2/1/jackal"",
  ""profile"": [
    ""http://iiif.io/api/image/2/level0.json"",
    {
      ""formats"": [""jpg""],
      ""qualities"": [""color""],
      ""supports"": [""sizeByWhListed""]
    }
  ],
  ""protocol"": ""http://iiif.io/api/image"",
  ""width"": 400,
  ""height"": 800,
  ""sizes"": [
    {""width"":100,""height"":200},
    {""width"":400,""height"":800}
  ]
}";
  // Act
  var actual = InfoJsonBuilder.GetImageApi2_1Level0(
    "https://test.example.com/iiif-img/2/1/jackal",
    new List<int[]> { new[] { 400, 800 }, new[] { 100, 200 } });

  // Assert
  var normalisedJson = actual.AsJson().Replace("\r\n", "\n");
  normalisedJson.Should().BeEquivalentTo(expected);
}

[Fact]
public void GetImageApi2_1Level1_ReturnsExpected()
{
  // Arrange
  var expected = @"{
  ""@context"": ""http://iiif.io/api/image/2/context.json"",
  ""@id"": ""https://test.example.com/iiif-img/2/1/jackal"",
  ""profile"": [
    ""http://iiif.io/api/image/2/level1.json"",
    {
      ""formats"": [""jpg""],
      ""qualities"": [
        ""native"",
        ""color"",
        ""gray""
      ],
      ""supports"": [
        ""regionByPct"",
        ""sizeByForcedWh"",
        ""sizeByWh"",
        ""sizeAboveFull"",
        ""rotationBy90s"",
        ""mirroring"",
        ""gray""
      ]
    }
  ],
  ""protocol"": ""http://iiif.io/api/image"",
  ""width"": 4200,
  ""height"": 8400,
  ""sizes"": [
    {""width"":100,""height"":200},
    {""width"":400,""height"":800},
    {""width"":1000,""height"":2000}
  ],
  ""tiles"": [
    {
      ""width"": 256,
      ""height"": 256,
      ""scaleFactors"": [
        1,
        2,
        4,
        8,
        16,
        32,
        64
      ]
    }
  ]
}";
  // Act
  var actual = InfoJsonBuilder.GetImageApi2_1Level1(
    "https://test.example.com/iiif-img/2/1/jackal",
    4200, 8400,
    new List<int[]> { new[] { 1000, 2000 }, new[] { 400, 800 }, new[] { 100, 200 } });

  // Assert
  var normalisedJson = actual.AsJson().Replace("\r\n", "\n");
  normalisedJson.Should().BeEquivalentTo(expected);
}

[Fact]
public void GetImageApi3_Level0_ReturnsExpected()
{
  // Arrange
  var expected = @"{
  ""@context"": ""http://iiif.io/api/image/3/context.json"",
  ""id"": ""https://test.example.com/iiif-img/2/1/jackal"",
  ""type"": ""ImageService3"",
  ""profile"": ""level0"",
  ""protocol"": ""http://iiif.io/api/image"",
  ""width"": 400,
  ""height"": 800,
  ""sizes"": [
    {""width"":100,""height"":200},
    {""width"":400,""height"":800}
  ],
  ""preferredFormats"": [""jpg""],
  ""extraFeatures"": [
    ""profileLinkHeader"",
    ""jsonldMediaType""
  ]
}";
  // Act
  var actual = InfoJsonBuilder.GetImageApi3_Level0(
    "https://test.example.com/iiif-img/2/1/jackal",
    new List<int[]> { new[] { 400, 800 }, new[] { 100, 200 } });

  // Assert
  var normalisedJson = actual.AsJson().Replace("\r\n", "\n");
  normalisedJson.Should().BeEquivalentTo(expected);
}
}