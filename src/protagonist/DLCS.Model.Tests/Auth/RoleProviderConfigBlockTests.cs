using System;
using DLCS.Model.Auth;

namespace DLCS.Model.Tests.Auth;

public class RoleProviderConfigBlockTests
{
    // {"default": {"config": "cas","target": "https://test.example/login","roles": "https://test.example/roles","logout": "https://test.example/logout"},"diff.host": {"config": "cas","target": "https://diff.host/login","roles": "https://diff.host/roles","logout": "https://diff.host/logout"}}
    const string MultiConfig =
        "ewoiZGVmYXVsdCI6IHsKImNvbmZpZyI6ICJjYXMiLAoidGFyZ2V0IjogImh0dHBzOi8vdGVzdC5leGFtcGxlL2xvZ2luIiwKInJvbGVzIjogImh0dHBzOi8vdGVzdC5leGFtcGxlL3JvbGVzIiwKImxvZ291dCI6ICJodHRwczovL3Rlc3QuZXhhbXBsZS9sb2dvdXQiCn0sCiJkaWZmLmhvc3QiOiB7CiJjb25maWciOiAiY2FzIiwKInRhcmdldCI6ICJodHRwczovL2RpZmYuaG9zdC9sb2dpbiIsCiJyb2xlcyI6ICJodHRwczovL2RpZmYuaG9zdC9yb2xlcyIsCiJsb2dvdXQiOiAiaHR0cHM6Ly9kaWZmLmhvc3QvbG9nb3V0Igp9Cn0=";
    
    // {"config": "cas","target": "https://test.example/login","roles": "https://test.example/roles","logout": "https://test.example/logout"}
    const string SingleConfig =
        "ewoiY29uZmlnIjogImNhcyIsCiJ0YXJnZXQiOiAiaHR0cHM6Ly90ZXN0LmV4YW1wbGUvbG9naW4iLAoicm9sZXMiOiAiaHR0cHM6Ly90ZXN0LmV4YW1wbGUvcm9sZXMiLAoibG9nb3V0IjogImh0dHBzOi8vdGVzdC5leGFtcGxlL2xvZ291dCIKfQ==";
    
    [Fact]
    public void FromBase64Json_SingleElement_SetsDefaultInDictionary()
    {
        // Arrange
        var expected = new RoleProviderConfiguration
        {
            Config = "cas",
            Target = new Uri("https://test.example/login"),
            Roles = new Uri("https://test.example/roles"),
            Logout = new Uri("https://test.example/logout"),
        };
        
        // Act
        var roleProviderConfigBlock = RoleProviderConfigBlock.FromBase64Json(SingleConfig);
        
        // Assert
        roleProviderConfigBlock.Configuration.Should().HaveCount(1);
        roleProviderConfigBlock.Configuration.Should()
            .ContainKey("default")
            .WhoseValue.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void FromBase64Json_MultiElement_SetsDictionary()
    {
        // Arrange
        var expectedDefault = new RoleProviderConfiguration
        {
            Config = "cas",
            Target = new Uri("https://test.example/login"),
            Roles = new Uri("https://test.example/roles"),
            Logout = new Uri("https://test.example/logout"),
        };
        
        var expectedOther = new RoleProviderConfiguration
        {
            Config = "cas",
            Target = new Uri("https://diff.host/login"),
            Roles = new Uri("https://diff.host/roles"),
            Logout = new Uri("https://diff.host/logout"),
        };
        
        // Act
        var roleProviderConfigBlock = RoleProviderConfigBlock.FromBase64Json(MultiConfig);
        
        // Assert
        roleProviderConfigBlock.Configuration.Should().HaveCount(2);
        roleProviderConfigBlock.Configuration.Should()
            .ContainKey("default")
            .WhoseValue.Should().BeEquivalentTo(expectedDefault);
        roleProviderConfigBlock.Configuration.Should()
            .ContainKey("diff.host")
            .WhoseValue.Should().BeEquivalentTo(expectedOther);
    }

    [Fact]
    public void GetForHost_ReturnsHostSpecific_IfFound()
    {
        // Arrange
        var expected = new RoleProviderConfiguration
        {
            Config = "cas",
            Target = new Uri("https://diff.host/login"),
            Roles = new Uri("https://diff.host/roles"),
            Logout = new Uri("https://diff.host/logout"),
        };
        
        var roleProviderConfigBlock = RoleProviderConfigBlock.FromBase64Json(MultiConfig);
        
        // Act
        var result = roleProviderConfigBlock.GetForHost("diff.host");
        
        // Assert
        result.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void GetForHost_ReturnsDefault_IfHostNotFound()
    {
        // Arrange
        var expected = new RoleProviderConfiguration
        {
            Config = "cas",
            Target = new Uri("https://test.example/login"),
            Roles = new Uri("https://test.example/roles"),
            Logout = new Uri("https://test.example/logout"),
        };
        
        var roleProviderConfigBlock = RoleProviderConfigBlock.FromBase64Json(MultiConfig);
        
        // Act
        var result = roleProviderConfigBlock.GetForHost("unknown.host");
        
        // Assert
        result.Should().BeEquivalentTo(expected);
    }
}