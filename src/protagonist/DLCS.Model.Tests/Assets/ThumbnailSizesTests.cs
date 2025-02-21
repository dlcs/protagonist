using System;
using System.Collections.Generic;
using DLCS.Model.Assets;

namespace DLCS.Model.Tests.Assets;

public class ThumbnailSizesTests
{
    [Fact]
    public void Empty_InitialisesSizes()
    {
        ThumbnailSizes.Empty.Auth.Should().BeEmpty();
        ThumbnailSizes.Empty.Open.Should().BeEmpty();
    }

    [Fact]
    public void Empty_GetAllSizes_Empty()
    {
        ThumbnailSizes.Empty.GetAllSizes().Should().BeEmpty();
        ThumbnailSizes.Empty.IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void CtorWithLists_Count_OpenAndAuth()
    {
        var actual = new ThumbnailSizes(
            new List<int[]> { new[] { 100, 200 }, new[] { 75, 150 }, },
            new List<int[]> { new[] { 500, 1000 }, });

        actual.Count.Should().Be(3);
    }
    
    [Fact]
    public void CtorWithLists_Count_OpenOnly()
    {
        var actual = new ThumbnailSizes(
            new List<int[]> { new[] { 100, 200 }, new[] { 75, 150 }, },
            new List<int[]>());

        actual.Count.Should().Be(2);
    }
    
    [Fact]
    public void CtorWithLists_Count_AuthOnly()
    {
        var actual = new ThumbnailSizes(
            new List<int[]>(),
            new List<int[]> { new[] { 500, 1000 }, new[] { 200, 400 }, });

        actual.Count.Should().Be(2);
    }

    [Fact]
    public void SizeClosestTo_Throws_IfEmpty()
    {
        Action action = () => ThumbnailSizes.Empty.SizeClosestTo(200, out _);
        action.Should()
            .ThrowExactly<InvalidOperationException>()
            .WithMessage("Cannot find closest size as ThumbnailSizes empty");
    }

    [Fact]
    public void SizeClosestTo_Returns_CorrectOpen()
    {
        var sizes = new ThumbnailSizes(
            new List<int[]> { new[] { 100, 200 }, new[] { 75, 150 }, },
            new List<int[]> { new[] { 500, 1000 }, });

        var closest = sizes.SizeClosestTo(350, out var isOpen);

        closest.Width.Should().Be(100);
        closest.Height.Should().Be(200);
        isOpen.Should().BeTrue();
    }
    
    [Fact]
    public void SizeClosestTo_Returns_CorrectAuth()
    {
        var sizes = new ThumbnailSizes(
            new List<int[]> { new[] { 100, 200 }, new[] { 75, 150 }, },
            new List<int[]> { new[] { 500, 1000 }, });

        var closest = sizes.SizeClosestTo(900, out var isOpen);

        closest.Width.Should().Be(500);
        closest.Height.Should().Be(1000);
        isOpen.Should().BeFalse();
    }
}