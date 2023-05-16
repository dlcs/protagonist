﻿using System;
using System.Text;
using DLCS.Core.Exceptions;
using DLCS.Core.Types;

namespace DLCS.Core.Tests.Types;

public class AssetIdTests
{
    [Fact]
    public void Ctor_Throws_IfLongerThan225()
    {
        var sb = new StringBuilder(215);
        for (var x = 0; x < 250; x++)
        {
            sb.Append('a');
        }

        Action action = () => new AssetId(10, 10, sb.ToString());
        action.Should()
            .Throw<InvalidAssetIdException>()
            .WithMessage("AssetId cannot be longer than 220")
            .And.Error.Should().Be(AssetIdError.TooLong);
    }
    
    [Fact]
    public void ToString_CorrectFormat()
    {
        var assetImageId = new AssetId(19, 4, "my-first-image");
        const string expected = "19/4/my-first-image";

        assetImageId.ToString().Should().Be(expected);
    }
    
    [Fact]
    public void FromString_ReturnsExpected()
    {
        const string assetId = "19/4/my-first-image";

        var assetImageId = AssetId.FromString(assetId);

        assetImageId.Customer.Should().Be(19);
        assetImageId.Space.Should().Be(4);
        assetImageId.Asset.Should().Be("my-first-image");
    }

    [Theory]
    [InlineData("image")]
    [InlineData("1/2/image/easrwt")]
    [InlineData("1/str/image")]
    [InlineData("str/2/image")]
    public void FromString_Throws_IfInvalidFormat(string assetId)
    {
        Action action = () => AssetId.FromString(assetId);
        action.Should()
            .Throw<InvalidAssetIdException>()
            .WithMessage($"AssetId '{assetId}' is invalid. Must be in format customer/space/asset")
            .And.Error.Should().Be(AssetIdError.InvalidFormat);
    }

    [Fact]
    public void CanDeconstruct()
    {
        var assetId = new AssetId(99, 100, "same");
        var (customer, space, asset) = assetId;

        customer.Should().Be(99);
        space.Should().Be(100);
        asset.Should().Be("same");
    }

    [Fact]
    public void Equals_Compares_Values()
    {
        var assetId1 = new AssetId(99, 100, "same");
        var assetId2 = new AssetId(99, 100, "same");

        assetId1.Equals(assetId2).Should().BeTrue();
    }
    
    [Theory]
    [InlineData("99/100/foo", "99/100/foo", true)]
    [InlineData("99/100/foo", "89/100/foo", false)]
    [InlineData("99/100/foo", "99/1/foo", false)]
    [InlineData("99/100/foo", "99/100/bar", false)]
    public void EqualsOperator_Compares_Values(string one, string two, bool expected)
    {
        var assetId1 = AssetId.FromString(one);
        var assetId2 = AssetId.FromString(two);

        (assetId1 == assetId2).Should().Be(expected);
    }

    [Fact]
    public void EqualsOperator_HandlesNull()
    {
        var assetId = new AssetId(1, 2, "foo");
        AssetId? nullAsset = null;

        (assetId == nullAsset).Should().BeFalse();
        (nullAsset == assetId).Should().BeFalse();
        (nullAsset == nullAsset).Should().BeTrue();
    }
    
    [Theory]
    [InlineData("99/100/foo", "99/100/foo", false)]
    [InlineData("99/100/foo", "89/100/foo", true)]
    [InlineData("99/100/foo", "99/1/foo", true)]
    [InlineData("99/100/foo", "99/100/bar", true)]
    public void NotEqualsOperator_Compares_Values(string one, string two, bool expected)
    {
        var assetId1 = AssetId.FromString(one);
        var assetId2 = AssetId.FromString(two);

        (assetId1 != assetId2).Should().Be(expected);
    }
    
    [Fact]
    public void NotEqualsOperator_HandlesNull()
    {
        var assetId = new AssetId(1, 2, "foo");
        AssetId? nullAsset = null;

        (assetId != nullAsset).Should().BeTrue();
        (nullAsset != assetId).Should().BeTrue();
        (nullAsset != nullAsset).Should().BeFalse();
    }

    [Fact]
    public void ExplicitConversion_FromString()
    {
        var assetId = (AssetId)"1/2/foo";

        assetId.Customer.Should().Be(1);
        assetId.Space.Should().Be(2);
        assetId.Asset.Should().Be("foo");

    }
}