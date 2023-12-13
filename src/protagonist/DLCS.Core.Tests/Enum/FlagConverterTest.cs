using System;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using DLCS.Core.Enum;

namespace DLCS.Core.Tests.Enum;

public class FlagConverterTest
{
     [Fact]
     public void ConvertFlagEnum_WritesEnum_WhenSerialized()
     {
          // Arrange
          var testObject = new TestObject()
          {
               TestFlagEnum = TestFlagConverterEnum.BlackBear | TestFlagConverterEnum.BrownBearFromDescription
          };

          // Act
          var serializedObject = JsonSerializer.Serialize(testObject);

          // Assert
          serializedObject.Should().Contain("BlackBear");
          serializedObject.Should().Contain("BrownBearDesc");
     }
     
     [Fact]
     public void ConvertFlagEnum_ReadsEnum_WhenDeserialized()
     {
          // Arrange
          var serializedObject = "{\"TestFlagEnum\":[\"BlackBear\",\"BrownBearDesc\"]}";

          // Act
          var testObject = JsonSerializer.Deserialize<TestObject>(serializedObject);

          // Assert
          testObject.TestFlagEnum.HasFlag(TestFlagConverterEnum.BlackBear).Should().BeTrue();
          testObject.TestFlagEnum.HasFlag(TestFlagConverterEnum.BrownBearFromDescription).Should().BeTrue();
          testObject.TestFlagEnum.HasFlag(TestFlagConverterEnum.PolarBear).Should().BeFalse();
          testObject.TestFlagEnum.HasFlag(TestFlagConverterEnum.BrownBear).Should().BeFalse();
     }
     
     [Fact]
     public void ConvertFlagEnum_NoError_WhenDeserializingNonExistentEnum()
     {
          // Arrange
          var serializedObject = "{\"TestFlagEnum\":[\"SunBear\"]}";

          // Act
          var testObject = JsonSerializer.Deserialize<TestObject>(serializedObject);

          // Assert
          testObject.TestFlagEnum.HasFlag(TestFlagConverterEnum.BlackBear).Should().BeFalse();
          testObject.TestFlagEnum.HasFlag(TestFlagConverterEnum.BrownBearFromDescription).Should().BeFalse();
          testObject.TestFlagEnum.HasFlag(TestFlagConverterEnum.PolarBear).Should().BeFalse();
          testObject.TestFlagEnum.HasFlag(TestFlagConverterEnum.BrownBear).Should().BeFalse();
     }
     
     public class TestObject
     {
          public TestFlagConverterEnum TestFlagEnum { get; set; }
     }
     
     [Flags]
     [JsonConverter(typeof(FlagConverter<TestFlagConverterEnum>))]
     public enum TestFlagConverterEnum
     {
          BlackBear = 1,
        
          BrownBear = 2,
        
          [Description("BrownBearDesc")]
          BrownBearFromDescription = 4,
          
          PolarBear = 6
     }
}