using DLCS.HydraModel;
using FluentAssertions;
using Xunit;

namespace DLCS.Model.Tests.Assets;

public class ImageQueryTests
{
    [Fact]
    public void ImageQuery_Can_Parse_From_String()
    {
        // arrange
        var s =
            @"{""string1"":""s1"",""string2"":""s2"",""string3"":""s3"",""number1"":1,""number2"":2,""number3"":3,""space"":99}";
        
        // act
        var iq = ImageQuery.Parse(s);
        
        // assert
        iq.Should().NotBeNull();
        iq.String1.Should().Be("s1");
        iq.String2.Should().Be("s2");
        iq.String3.Should().Be("s3");
        iq.Number1.Should().Be(1);
        iq.Number2.Should().Be(2);
        iq.Number3.Should().Be(3);
    }
}