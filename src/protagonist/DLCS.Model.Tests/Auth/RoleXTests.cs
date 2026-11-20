using System.Collections.Generic;
using DLCS.Model.Auth;

namespace DLCS.Model.Tests.Auth;

public class RoleXTests
{
    [Theory]
    [InlineData("https://api.dlcs.io/customers/2/roles/ip:reading-room")]
    [InlineData("https://api.dlcs.io/customers/2/roles/ip:reading-room/")]
    [InlineData("ip:reading-room")]
    public void IsIpRole_True_IfLastSegmentHasIpPrefix(string role)
        => role.IsIpRole().Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://api.dlcs.io/customers/2/roles/clickthrough")]
    [InlineData("https://api.dlcs.io/customers/2/roles/appointment:token-based")]
    [InlineData("https://api.dlcs.io/customers/2/ip:roles/reading-room")]
    [InlineData("https://api.dlcs.io/customers/2/roles/IP:reading-room")]
    [InlineData("https://api.dlcs.io/customers/2/roles/staff-ip:reading-room")]
    public void IsIpRole_False_IfLastSegmentDoesNotHaveIpPrefix(string role)
        => role.IsIpRole().Should().BeFalse();

    [Fact]
    public void ContainsIpRole_True_IfAnyIpRole()
        => new[] { "https://api.dlcs.io/customers/2/roles/clickthrough", "https://api.dlcs.io/customers/2/roles/ip:rr" }
            .ContainsIpRole().Should().BeTrue();

    [Fact]
    public void ContainsIpRole_False_IfNoIpRole()
        => new[] { "https://api.dlcs.io/customers/2/roles/clickthrough" }.ContainsIpRole().Should().BeFalse();

    [Fact]
    public void ContainsIpRole_False_IfNull()
        => ((IEnumerable<string>)null).ContainsIpRole().Should().BeFalse();

    [Theory]
    [InlineData("https://api.dlcs.io/customers/2/roles/appointment:token-based")]
    [InlineData("https://api.dlcs.io/customers/2/roles/appointment:token-based/")]
    [InlineData("appointment:token-based")]
    public void IsAppointmentRole_True_IfLastSegmentHasAppointmentPrefix(string role)
        => role.IsAppointmentRole().Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://api.dlcs.io/customers/2/roles/clickthrough")]
    [InlineData("https://api.dlcs.io/customers/2/roles/ip:reading-room")]
    [InlineData("https://api.dlcs.io/customers/2/appointment:roles/x")]
    [InlineData("https://api.dlcs.io/customers/2/roles/Appointment:x")]
    public void IsAppointmentRole_False_IfLastSegmentDoesNotHaveAppointmentPrefix(string role)
        => role.IsAppointmentRole().Should().BeFalse();

    [Fact]
    public void ContainsAppointmentRole_True_IfAnyAppointmentRole()
        => new[] { "https://api.dlcs.io/customers/2/roles/clickthrough", "https://api.dlcs.io/customers/2/roles/appointment:a" }
            .ContainsAppointmentRole().Should().BeTrue();

    [Fact]
    public void ContainsAppointmentRole_False_IfNull()
        => ((IEnumerable<string>)null).ContainsAppointmentRole().Should().BeFalse();

    private const string Ip = "https://api.dlcs.io/customers/2/roles/ip:rr";
    private const string Appointment = "https://api.dlcs.io/customers/2/roles/appointment:a";
    private const string Staff = "https://api.dlcs.io/customers/2/roles/staff";

    [Theory]
    [InlineData(null, new string[0])]
    [InlineData(" + ", new string[0])]
    [InlineData("a", new[] { "a" })]
    [InlineData("a+b", new[] { "a", "b" })]
    [InlineData("a b", new[] { "a", "b" })]
    [InlineData(" a ++ b+a", new[] { "a", "b" })]
    public void GetEntryRoles_SplitsOnPlusAndWhitespace(string? entry, string[] expected)
        => entry.GetEntryRoles().Should().Equal(expected);

    [Fact]
    public void ContainsIpRole_True_IfIpRoleInEntry()
        => new[] { Staff, $"{Appointment}+{Ip}" }.ContainsIpRole().Should().BeTrue();

    [Fact]
    public void ContainsAppointmentRole_True_IfAppointmentRoleInEntry()
        => new[] { $"{Appointment}+{Ip}" }.ContainsAppointmentRole().Should().BeTrue();

    [Fact]
    public void HasIpOnlyEntry_True_IfAnyEntryAllIpRoles()
        => new[] { $"{Ip}+{Staff}", $"{Ip}+https://api.dlcs.io/customers/2/roles/ip:other" }
            .HasIpOnlyEntry().Should().BeTrue();

    [Fact]
    public void HasIpOnlyEntry_False_IfIpRolesOnlyInMixedEntries()
        => new[] { $"{Ip}+{Staff}", Appointment }.HasIpOnlyEntry().Should().BeFalse();

    [Fact]
    public void HasIpOnlyEntry_False_IfNullOrEmptyEntries()
    {
        ((IEnumerable<string>)null).HasIpOnlyEntry().Should().BeFalse();
        new[] { "", " + " }.HasIpOnlyEntry().Should().BeFalse();
    }
}
