using DLCS.Model.Policies;
using FluentAssertions;
using Xunit;

namespace DLCS.Model.Tests.Policies;

public class ImageOptimisationPolicyXTests
{
    [Theory]
    [InlineData("video-max")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void IsNoOpIdentifier_False(string id)
    {
        KnownImageOptimisationPolicy.IsNoOpIdentifier(id).Should().BeFalse();
    }
    
    [Fact]
    public void IsNoOpIdentifier_True()
    {
        KnownImageOptimisationPolicy.IsNoOpIdentifier("none").Should().BeTrue();
    }
    
    [Theory]
    [InlineData("video-max")]
    [InlineData("")]
    [InlineData(" ")]
    public void IsUseOriginal_False(string id)
    {
        var policy = new ImageOptimisationPolicy { Id = id };

        policy.IsUseOriginal().Should().BeFalse();
    }
    
    [Fact]
    public void IsUseOriginal_True()
    {
        var policy = new ImageOptimisationPolicy { Id = "use-original" };

        policy.IsUseOriginal().Should().BeTrue();
    }
    
    [Theory]
    [InlineData("video-max")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void IsUseOriginalIdentifier_False(string id)
    {
        KnownImageOptimisationPolicy.IsUseOriginalIdentifier(id).Should().BeFalse();
    }
    
    [Fact]
    public void IsUseOriginalIdentifier_True()
    {
        KnownImageOptimisationPolicy.IsUseOriginalIdentifier("use-original").Should().BeTrue();
    }
}