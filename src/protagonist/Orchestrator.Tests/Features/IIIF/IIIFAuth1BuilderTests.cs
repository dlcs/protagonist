using System;
using System.Collections.Generic;
using DLCS.Core.Types;
using DLCS.Model.Auth;
using DLCS.Model.Auth.Entities;
using IIIF.Auth.V1;
using IIIF.Presentation.V2.Strings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Settings;

namespace Orchestrator.Tests.Features.IIIF;

public class IIIFAuth1BuilderTests
{
    private readonly IAuthServicesRepository authServicesRepository;

    public IIIFAuth1BuilderTests()
    {
        authServicesRepository = A.Fake<IAuthServicesRepository>();
    }
    
    private IIIFAuth1Builder GetSut(bool throwIfNotFound = false)
    {
        var authOptions = Options.Create(new AuthSettings
        {
            SupportedAccessCookieProfiles = new List<string>
            {
                "http://iiif.io/api/auth/0/login/clickthrough"
            },
            ThrowIfUnsupportedProfile = throwIfNotFound
        });
        
        return new IIIFAuth1Builder(authServicesRepository, authOptions, new NullLogger<IIIFAuth1Builder>());
    }

    [Fact]
    public async Task GetAuthServicesForAsset_Null_IfUnableToFindAuthServices()
    {
        // Arrange
        var asset = GetAsset();
        var sut = GetSut();
        
        // Act 
        var result = await sut.GetAuthServicesForAsset(asset.AssetId, asset.Roles);
        
        // Assert
        result.Should().BeNull();
    }
    
    [Theory]
    [InlineData("http://iiif.io/api/auth/1/login/clickthrough")]
    [InlineData("http://iiif.io/api/auth/3/clickthrough")]
    public async Task GetAuthServicesForAsset_ReturnsNull_IfParentProfileUnknown_AndThrowIfUnsupportedFalse(string profile)
    {
        // Arrange
        var asset = GetAsset();
        const string roleName = "secret";
        asset.Roles = new List<string> { roleName };
        A.CallTo(() => authServicesRepository.GetAuthServicesForRole(99, roleName))
            .Returns(new List<AuthService>
            {
                new() { Name = "The-Parent", Label = "Parent", Description = "Parent Description", Profile = profile }
            });
        var sut = GetSut();
        
        // Act 
        var result = await sut.GetAuthServicesForAsset(asset.AssetId, asset.Roles);
        
        // Assert
        result.Should().BeNull();
    }
    
    [Theory]
    [InlineData("http://iiif.io/api/auth/1/login/clickthrough")]
    [InlineData("http://iiif.io/api/auth/3/clickthrough")]
    public async Task GetAuthServicesForAsset_Throws_IfParentProfileUnknown_AndThrowIfUnsupportedTrue(string profile)
    {
        // Arrange
        var asset = GetAsset();
        const string roleName = "secret";
        asset.Roles = new List<string> { roleName };
        A.CallTo(() => authServicesRepository.GetAuthServicesForRole(99, roleName))
            .Returns(new List<AuthService>
            {
                new() { Name = "The-Parent", Label = "Parent", Description = "Parent Description", Profile = profile }
            });
        var sut = GetSut(true);
        
        // Act 
        Func<Task> action = () => sut.GetAuthServicesForAsset(asset.AssetId, asset.Roles);
        
        // Assert
        await action.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage($"Unsupported AuthService profile type: {profile}");
    }
    
    [Theory]
    [InlineData("http://iiif.io/api/auth/1/login")]
    [InlineData("http://iiif.io/api/auth/1/clickthrough")]
    [InlineData("http://iiif.io/api/auth/1/kiosk")]
    [InlineData("http://iiif.io/api/auth/1/external")]
    public async Task GetAuthServicesForAsset_ReturnsCookieServiceWithNoChildren_IfNoChildServices_AuthCookie1(string profile)
    {
        // Arrange
        var asset = GetAsset();
        const string roleName = "secret";
        asset.Roles = new List<string> { roleName };
        A.CallTo(() => authServicesRepository.GetAuthServicesForRole(99, roleName))
            .Returns(new List<AuthService>
            {
                new() { Name = "The-Parent", Label = "Parent", Description = "Parent Description", Profile = profile }
            });
        var sut = GetSut();
        
        // Act 
        var result = await sut.GetAuthServicesForAsset(asset.AssetId, asset.Roles);
        
        // Assert
        result.Id.Should().Be("The-Parent");
        var authCookieSvc = result as AuthCookieService;
        authCookieSvc.Label.LanguageValues
            .Should().HaveCount(1)
            .And.Subject.Should().OnlyContain(v => v.Value == "Parent");
        authCookieSvc.Description.LanguageValues
            .Should().HaveCount(1)
            .And.Subject.Should().OnlyContain(v => v.Value == "Parent Description");
        authCookieSvc.Service.Should().BeNullOrEmpty();
    }
    
    [Theory]
    [InlineData("http://iiif.io/api/auth/0/login", "Auth spec login")]
    [InlineData("http://iiif.io/api/auth/0/clickthrough", "Auth spec clickthrough")]
    [InlineData("http://iiif.io/api/auth/0/kiosk", "Auth spec kiosk")]
    [InlineData("http://iiif.io/api/auth/0/external", "Auth spec external")]
    [InlineData("http://iiif.io/api/auth/0/login/clickthrough", "Not standard but in supported list")]
    public async Task GetAuthServicesForAsset_ReturnsCookieServiceWithNoChildren_IfNoChildServices_AuthCookie0(string profile, string reason)
    {
        // Arrange
        var asset = GetAsset();
        const string roleName = "secret";
        asset.Roles = new List<string> { roleName };
        A.CallTo(() => authServicesRepository.GetAuthServicesForRole(99, roleName))
            .Returns(new List<AuthService>
            {
                new() { Name = "The-Parent", Label = "Parent", Description = "Parent Description", Profile = profile }
            });
        var sut = GetSut();
        
        // Act 
        var result = await sut.GetAuthServicesForAsset(asset.AssetId, asset.Roles);
        
        // Assert
        result.Id.Should().Be("The-Parent");
        var authCookieSvc = result as global::IIIF.Auth.V0.AuthCookieService;
        authCookieSvc.Profile.Should().Be(profile, reason);
        authCookieSvc.Label.LanguageValues
            .Should().HaveCount(1)
            .And.Subject.Should().OnlyContain(v => v.Value == "Parent");
        authCookieSvc.Description.LanguageValues
            .Should().HaveCount(1)
            .And.Subject.Should().OnlyContain(v => v.Value == "Parent Description");
        authCookieSvc.Service.Should().BeNullOrEmpty();
    }
    
    [Fact]
    public async Task GetAuthServicesForAsset_ReturnsCookieServiceWithServices_IfChildServices()
    {
        // Arrange
        var asset = GetAsset();
        const string roleName = "secret";
        asset.Roles = new List<string> { roleName };
        A.CallTo(() => authServicesRepository.GetAuthServicesForRole(99, roleName))
            .Returns(new List<AuthService>
            {
                new()
                {
                    Customer = 99, Name = "The-Parent", Label = "Parent", Description = "Parent Description",
                    Profile = "http://iiif.io/api/auth/1/clickthrough"
                },
                new()
                {
                    Customer = 99, Name = "The-Logout", Label = "Logout", Description = "Logout Description",
                    Profile = "http://iiif.io/api/auth/1/logout"
                },
                new() { Name = "The-Token", Profile = "http://iiif.io/api/auth/1/token" }
            });

        var authLogoutService = new AuthLogoutService
        {
            Id = "The-Parent/logout",
            Label = new MetaDataValue("Logout"),
            Description = new MetaDataValue("Logout Description"),
        };
        
        var authTokenService = new AuthTokenService
        {
            Id = "The-Token",
        };
        var sut = GetSut();
        
        // Act 
        var result = await sut.GetAuthServicesForAsset(asset.AssetId, asset.Roles);
        
        // Assert
        result.Id.Should().Be("The-Parent");
        var authCookieSvc = result as AuthCookieService;
        authCookieSvc.Label.LanguageValues
            .Should().HaveCount(1)
            .And.Subject.Should().OnlyContain(v => v.Value == "Parent");
        authCookieSvc.Description.LanguageValues
            .Should().HaveCount(1)
            .And.Subject.Should().OnlyContain(v => v.Value == "Parent Description");

        authCookieSvc.Service.Should().HaveCount(2);
        authCookieSvc.Service[0].Should().BeEquivalentTo(authLogoutService);
        authCookieSvc.Service[1].Should().BeEquivalentTo(authTokenService);
    }
    
    [Theory]
    [InlineData("http://iiif.io/api/auth/3/logout")]
    [InlineData("http://iiif.io/api/auth/3/token")]
    public async Task GetAuthServicesForAsset_NullChildren_IfNonLevel1ChildServiceFound(string profile)
    {
        // Arrange
        var asset = GetAsset();
        const string roleName = "secret";
        asset.Roles = new List<string> { roleName };
        A.CallTo(() => authServicesRepository.GetAuthServicesForRole(99, roleName))
            .Returns(new List<AuthService>
            {
                new()
                {
                    Customer = 99, Name = "The-Parent", Label = "Parent", Description = "Parent Description",
                    Profile = "http://iiif.io/api/auth/1/clickthrough"
                },
                new() { Profile = profile },
            });
        var sut = GetSut();
        
        // Act 
        var result = await sut.GetAuthServicesForAsset(asset.AssetId, asset.Roles);
        
        // Assert
        var authCookieSvc = result as AuthCookieService;
        authCookieSvc.Service.Should().BeNullOrEmpty();
    }
    
    private OrchestrationImage GetAsset() => new() { AssetId = new AssetId(99, 1, "test-asset") };
}