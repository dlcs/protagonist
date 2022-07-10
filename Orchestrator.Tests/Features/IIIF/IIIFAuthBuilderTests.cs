using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Auth;
using DLCS.Model.Auth.Entities;
using FakeItEasy;
using FluentAssertions;
using IIIF.Auth.V1;
using IIIF.Presentation.V2.Strings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Settings;
using Xunit;

namespace Orchestrator.Tests.Features.IIIF;

public class IIIFAuthBuilderTests
{
    private readonly IAuthServicesRepository authServicesRepository;
    private readonly IIIFAuthBuilder sut;

    public IIIFAuthBuilderTests()
    {
        authServicesRepository = A.Fake<IAuthServicesRepository>();
        sut = new IIIFAuthBuilder(authServicesRepository, new NullLogger<IIIFAuthBuilder>());
    }

    [Fact]
    public async Task GetAuthCookieServiceForAsset_Null_IfUnableToFindAuthServices()
    {
        // Arrange
        var asset = GetAsset();
        
        // Act 
        var result = await sut.GetAuthCookieServiceForAsset(asset);
        
        // Assert
        result.Should().BeNull();
    }
    
    [Fact]
    public async Task GetAuthCookieServiceForAsset_ReturnsCookieServiceWithNoChildren_IfNoChildServices()
    {
        // Arrange
        var asset = GetAsset();
        const string roleName = "secret";
        asset.Roles = new List<string> { roleName };
        A.CallTo(() => authServicesRepository.GetAuthServicesForRole(99, roleName))
            .Returns(new List<AuthService>
                { new() { Name = "The-Parent", Label = "Parent", Description = "Parent Description" } });
        
        // Act 
        var result = await sut.GetAuthCookieServiceForAsset(asset);
        
        // Assert
        result.Id.Should().Be("The-Parent");
        result.Label.LanguageValues
            .Should().HaveCount(1)
            .And.Subject.Should().OnlyContain(v => v.Value == "Parent");
        result.Description.LanguageValues
            .Should().HaveCount(1)
            .And.Subject.Should().OnlyContain(v => v.Value == "Parent Description");
        result.Service.Should().BeNullOrEmpty();
    }
    
    [Fact]
    public async Task GetAuthCookieServiceForAsset_ReturnsCookieServiceWithServices_IfChildServices()
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
                    Profile = "http://iiif.io/api/auth/1/login/clickthrough"
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
        
        // Act 
        var result = await sut.GetAuthCookieServiceForAsset(asset);
        
        // Assert
        result.Id.Should().Be("The-Parent");
        result.Label.LanguageValues
            .Should().HaveCount(1)
            .And.Subject.Should().OnlyContain(v => v.Value == "Parent");
        result.Description.LanguageValues
            .Should().HaveCount(1)
            .And.Subject.Should().OnlyContain(v => v.Value == "Parent Description");

        result.Service.Should().HaveCount(2);
        result.Service[0].Should().BeEquivalentTo(authLogoutService);
        result.Service[1].Should().BeEquivalentTo(authTokenService);
    }
    
    [Theory]
    [InlineData("http://iiif.io/api/auth/0/logout")]
    [InlineData("http://iiif.io/api/auth/3/logout")]
    [InlineData("http://iiif.io/api/auth/0/token")]
    [InlineData("http://iiif.io/api/auth/3/token")]
    public void GetAuthCookieServiceForAsset_Throws_IfNonLevel1ChildServiceFound(string profile)
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
                    Profile = "http://iiif.io/api/auth/1/login/clickthrough"
                },
                new() { Profile = profile },
            });
        
        // Act 
        Func<Task> action = () => sut.GetAuthCookieServiceForAsset(asset);
        
        // Assert
        action.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Encountered unknown auth service for asset 99/1/test-asset");
    }
    
    private OrchestrationImage GetAsset() => new() { AssetId = new AssetId(99, 1, "test-asset") };
}