using DLCS.Core.Collections;

namespace DLCS.Core.Tests.Collections;

public class StringArraysTests
{
    [Fact]
    public void EnsureString_Adds_NewString()
    {
        var before = new string[] { };

        var ensured = StringArrays.EnsureString(before, "test");

        ensured.Should().Equal("test");
    }

    [Fact]
    public void EnsureString_Adds_AdditionalString()
    {
        var before = new[] { "already" };

        var ensured = StringArrays.EnsureString(before, "test");

        ensured.Should().Equal("already", "test");
    }

    [Fact]
    public void EnsureString_DoesNotAdd_AdditionalString()
    {
        var before = new[] { "already" };

        var ensured = StringArrays.EnsureString(before, "already");

        ensured.Should().Equal("already");
    }

    [Fact]
    public void RemoveString_RemovesString()
    {
        var before = new[] { "a", "b" };

        var ensured = StringArrays.RemoveString(before, "b");

        ensured.Should().Equal("a");
    }

    [Fact]
    public void RemoveString_RemovesString_InRightOrder()
    {
        var before = new[] { "a", "b", "c" };

        var ensured = StringArrays.RemoveString(before, "b");

        ensured.Should().Equal("a", "c");
    }

    [Fact]
    public void RemoveString_RemovesString_LeavesEmpty()
    {
        var before = new[] { "before" };

        var ensured = StringArrays.RemoveString(before, "before");

        ensured.Should().BeEmpty();
    }

    [Fact]
    public void RemoveString_DoesntRemove()
    {
        var before = new[] { "a", "b" };

        var ensured = StringArrays.RemoveString(before, "c");

        ensured.Should().Equal("a", "b");
    }
}