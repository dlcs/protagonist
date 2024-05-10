using System;
using System.Collections.Generic;
using DLCS.Model.Policies;
using IIIF.ImageApi;

namespace DLCS.Model.Tests.Policies;

public class DeliveryChannelPolicyXTests
{
    [Fact]
    public void ThumbsDataAsSizeParameters_Throws_IfPolicyNotThumbs()
    {
        var policy = new DeliveryChannelPolicy { Channel = "iiif-img" };

        Action action = () => policy.ThumbsDataAsSizeParameters();
        action.Should().ThrowExactly<InvalidOperationException>();
    }

    [Fact]
    public void ThumbsDataAsSizeParameters_ReturnsSizeParameters()
    {
        // Arrange
        var expected = new List<SizeParameter>
        {
            SizeParameter.Parse("200,"), SizeParameter.Parse(",200"), SizeParameter.Parse("!400,400")
        };

        var policy = new DeliveryChannelPolicy { Channel = "thumbs", PolicyData = "[\"200,\",\",200\",\"!400,400\"]" };
        
        // Act
        var actual = policy.ThumbsDataAsSizeParameters();
        
        // Assert
        actual.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void PolicyDataAs_ReturnsExpected()
    {
        // Arrange
        var expected = new List<string> { "200,", ",200", "!400,400" };
        var policy = new DeliveryChannelPolicy { Channel = "thumbs", PolicyData = "[\"200,\",\",200\",\"!400,400\"]" };
        
        // Act
        var actual = policy.PolicyDataAs<List<string>>();
        
        // Assert
        actual.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void PolicyDataAs_Throws_IfInvalidConversion()
    {
        // Arrange
        var policy = new DeliveryChannelPolicy { Channel = "thumbs", PolicyData = "{ \"foo\": \"bar\"}" };
        
        // Act
        Action action = () => policy.PolicyDataAs<List<string>>();
        action.Should().ThrowExactly<InvalidOperationException>();
    }
}