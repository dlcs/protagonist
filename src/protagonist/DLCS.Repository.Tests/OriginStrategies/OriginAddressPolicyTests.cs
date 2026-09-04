using System;
using System.Net;
using DLCS.Repository.OriginStrategies;

namespace DLCS.Repository.Tests.OriginStrategies;

public class OriginAddressPolicyTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.1.2.3")]
    [InlineData("127.255.255.254")]
    [InlineData("::1")]
    [InlineData("169.254.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("fe80::1")]
    [InlineData("febf:ffff::1")]
    [InlineData("fc00::1")]
    [InlineData("fd00:ec2::254")]
    [InlineData("fdff:ffff::1")]
    [InlineData("0.0.0.0")]
    [InlineData("0.1.2.3")]
    [InlineData("::")]
    public void GetBlockingRange_ReturnsRange_ForDefaultBlockedAddress(string address)
    {
        var sut = GetSut();

        sut.GetBlockingRange(IPAddress.Parse(address)).Should().NotBeNull();
    }

    [Theory]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("::ffff:0.0.0.0")]
    public void GetBlockingRange_ReturnsRange_ForIPv4MappedToIPv6BlockedAddress(string address)
    {
        var sut = GetSut();

        sut.GetBlockingRange(IPAddress.Parse(address)).Should().NotBeNull();
    }

    [Theory]
    [InlineData("1.0.0.1")]
    [InlineData("8.8.8.8")]
    [InlineData("128.0.0.1")]
    [InlineData("126.255.255.255")]
    [InlineData("169.253.0.1")]
    [InlineData("169.255.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("192.168.0.1")]
    [InlineData("2001:4860:4860::8888")]
    [InlineData("fec0::1")]
    [InlineData("fb00::1")]
    public void GetBlockingRange_ReturnsNull_ForAllowedAddress(string address)
    {
        var sut = GetSut();

        sut.GetBlockingRange(IPAddress.Parse(address)).Should().BeNull();
    }

    [Fact]
    public void GetBlockingRange_ReturnsRange_ForConfiguredRange()
    {
        var sut = GetSut(blocked: ["10.0.0.0/8", "2001:db8::/32"]);

        sut.GetBlockingRange(IPAddress.Parse("10.1.2.3")).Should().Be(IPNetwork.Parse("10.0.0.0/8"));
        sut.GetBlockingRange(IPAddress.Parse("2001:db8::1")).Should().Be(IPNetwork.Parse("2001:db8::/32"));
        sut.GetBlockingRange(IPAddress.Parse("::ffff:10.1.2.3")).Should().Be(IPNetwork.Parse("10.0.0.0/8"));
    }

    [Fact]
    public void GetBlockingRange_ReturnsRange_ForDefaultBlockedAddress_IfRangesConfigured()
    {
        var sut = GetSut(blocked: ["10.0.0.0/8"]);

        sut.GetBlockingRange(IPAddress.Loopback).Should().Be(IPNetwork.Parse("127.0.0.0/8"));
    }

    [Fact]
    public void GetBlockingRange_HandlesConfiguredRangeWithHostBitsSet()
    {
        var sut = GetSut(blocked: ["10.1.2.3/8"]);

        sut.GetBlockingRange(IPAddress.Parse("10.9.9.9")).Should().Be(IPNetwork.Parse("10.0.0.0/8"));
    }

    [Theory]
    [InlineData("not-a-range")]
    [InlineData("10.0.0.0")]
    [InlineData("10.0.0.0/33")]
    public void Ctor_Throws_IfConfiguredBlockedRangeInvalid(string range)
    {
        Action action = () => _ = GetSut(blocked: [range]);

        action.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("not-a-range")]
    [InlineData("10.0.0.0")]
    [InlineData("10.0.0.0/33")]
    public void Ctor_Throws_IfConfiguredAllowedRangeInvalid(string range)
    {
        Action action = () => _ = GetSut(allowed: [range]);

        action.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("fd12::1")]
    [InlineData("0.0.0.0")]
    public void GetBlockingRange_ReturnsNull_IfDefaultBlockedAddressExplicitlyAllowed(string address)
    {
        var sut = GetSut(allowed: ["127.0.0.0/8", "::1/128", "fc00::/7", "0.0.0.0/8"]);

        sut.GetBlockingRange(IPAddress.Parse(address)).Should().BeNull();
    }

    [Fact]
    public void GetBlockingRange_ReturnsNull_IfBlockedAddressExplicitlyAllowed()
    {
        var sut = GetSut(blocked: ["10.0.0.0/8"], allowed: ["10.1.0.0/16"]);

        sut.GetBlockingRange(IPAddress.Parse("10.1.2.3")).Should().BeNull();
    }

    [Fact]
    public void GetBlockingRange_ReturnsRange_ForAddressOutsideAllowedRange()
    {
        var sut = GetSut(blocked: ["10.0.0.0/8"], allowed: ["10.1.0.0/16"]);

        sut.GetBlockingRange(IPAddress.Parse("10.2.3.4")).Should().Be(IPNetwork.Parse("10.0.0.0/8"));
        sut.GetBlockingRange(IPAddress.Loopback).Should().Be(IPNetwork.Parse("127.0.0.0/8"));
    }

    [Fact]
    public void GetBlockingRange_ReturnsNull_ForIPv4MappedToIPv6FormOfAllowedAddress()
    {
        var sut = GetSut(allowed: ["127.0.0.0/8"]);

        sut.GetBlockingRange(IPAddress.Parse("::ffff:127.0.0.1")).Should().BeNull();
    }

    [Theory]
    [InlineData("169.254.169.254")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("fd00:ec2::254")]
    public void GetBlockingRange_ReturnsRange_ForInstanceMetadataAddress_EvenIfAllowed(string address)
    {
        var sut = GetSut(allowed: ["0.0.0.0/0", "::/0"]);

        sut.GetBlockingRange(IPAddress.Parse(address)).Should().NotBeNull();
    }

    private static OriginAddressPolicy GetSut(string[]? blocked = null, string[]? allowed = null)
        => new(new OriginStrategySettings { BlockedIpRanges = blocked ?? [], AllowedIpRanges = allowed ?? [] });
}
