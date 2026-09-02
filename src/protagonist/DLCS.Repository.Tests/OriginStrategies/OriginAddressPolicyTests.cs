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
    public void GetBlockingRange_ReturnsRange_ForAlwaysBlockedAddress(string address)
    {
        var sut = new OriginAddressPolicy();

        sut.GetBlockingRange(IPAddress.Parse(address)).Should().NotBeNull();
    }

    [Theory]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("::ffff:0.0.0.0")]
    public void GetBlockingRange_ReturnsRange_ForIPv4MappedToIPv6BlockedAddress(string address)
    {
        var sut = new OriginAddressPolicy();

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
        var sut = new OriginAddressPolicy();

        sut.GetBlockingRange(IPAddress.Parse(address)).Should().BeNull();
    }

    [Fact]
    public void GetBlockingRange_ReturnsRange_ForConfiguredRange()
    {
        var sut = new OriginAddressPolicy(["10.0.0.0/8", "2001:db8::/32"]);

        sut.GetBlockingRange(IPAddress.Parse("10.1.2.3")).Should().Be(IPNetwork.Parse("10.0.0.0/8"));
        sut.GetBlockingRange(IPAddress.Parse("2001:db8::1")).Should().Be(IPNetwork.Parse("2001:db8::/32"));
        sut.GetBlockingRange(IPAddress.Parse("::ffff:10.1.2.3")).Should().Be(IPNetwork.Parse("10.0.0.0/8"));
    }

    [Fact]
    public void GetBlockingRange_ReturnsRange_ForAlwaysBlockedAddress_IfRangesConfigured()
    {
        var sut = new OriginAddressPolicy(["10.0.0.0/8"]);

        sut.GetBlockingRange(IPAddress.Loopback).Should().Be(IPNetwork.Parse("127.0.0.0/8"));
    }

    [Fact]
    public void GetBlockingRange_HandlesConfiguredRangeWithHostBitsSet()
    {
        var sut = new OriginAddressPolicy(["10.1.2.3/8"]);

        sut.GetBlockingRange(IPAddress.Parse("10.9.9.9")).Should().Be(IPNetwork.Parse("10.0.0.0/8"));
    }

    [Theory]
    [InlineData("not-a-range")]
    [InlineData("10.0.0.0")]
    [InlineData("10.0.0.0/33")]
    public void Ctor_Throws_IfConfiguredRangeInvalid(string range)
    {
        Action action = () => _ = new OriginAddressPolicy([range]);

        action.Should().Throw<FormatException>();
    }
}
