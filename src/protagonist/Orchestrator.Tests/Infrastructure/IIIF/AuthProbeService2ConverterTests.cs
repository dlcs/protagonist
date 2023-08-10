using System.Collections.Generic;
using IIIF;
using IIIF.Auth.V2;
using IIIF.Presentation.V3.Strings;
using Orchestrator.Infrastructure.IIIF;

namespace Orchestrator.Tests.Infrastructure.IIIF;

public class AuthProbeService2ConverterTests
{
    [Fact]
    public void ToEmbeddedService_AuthProbeService2_HandlesNoChildServices()
    {
        // Arrange
        var authProbeService = new AuthProbeService2
        {
            Id = "http://example/auth2",
            Label = new LanguageMap("en", "test"),
        };

        var expected = new AuthProbeService2
        {
            Id = "http://example/auth2",
        };
        
        // Act
        var actual = authProbeService.ToEmbeddedService();
        
        // Assert
        actual.Should().BeEquivalentTo(expected);
        actual.Context.Should().BeNull();
    }
    
    [Fact]
    public void ToEmbeddedService_AuthProbeService2_OutputsChildAuthAccessService_SingleService()
    {
        // Arrange
        var authProbeService = new AuthProbeService2
        {
            Id = "http://example/auth2",
            Label = new LanguageMap("en", "test"),
            Service = new List<IService>
            {
                new AuthAccessService2
                {
                    Id = "http://example/access2",
                    Profile = "active",
                    Label = new LanguageMap("en", "test"),
                    Service = new List<IService>
                    {
                        new AuthAccessTokenService2
                        {
                            Id = "http://example/token2",
                            ErrorNote = new LanguageMap("en", "test"),
                        },
                        new AuthLogoutService2
                        {
                            Id = "http://example/logout2",
                            Label = new LanguageMap("en", "test"),
                        }
                    }
                }
            }
        };

        var expected = new AuthProbeService2
        {
            Id = "http://example/auth2",
            Service = new List<IService>
            {
                new AuthAccessService2 { Id = "http://example/access2", }
            }
        };
        
        // Act
        var actual = authProbeService.ToEmbeddedService();
        
        // Assert
        actual.Should().BeEquivalentTo(expected);
        actual.Context.Should().BeNull();
    }
    
    [Fact]
    public void ToEmbeddedService_AuthProbeService2_OutputsChildAuthAccessService_MultipleServices()
    {
        // Arrange
        var authProbeService = new AuthProbeService2
        {
            Id = "http://example/auth2",
            Label = new LanguageMap("en", "test"),
            Service = new List<IService>
            {
                new AuthAccessService2
                {
                    Id = "http://example/access2",
                    Profile = "active",
                    Label = new LanguageMap("en", "test"),
                    Service = new List<IService>
                    {
                        new AuthAccessTokenService2
                        {
                            Id = "http://example/token2",
                            ErrorNote = new LanguageMap("en", "test"),
                        },
                        new AuthLogoutService2
                        {
                            Id = "http://example/logout2",
                            Label = new LanguageMap("en", "test"),
                        }
                    }
                },
                new AuthAccessService2
                {
                    Id = "http://example/access2/again",
                    Profile = "active",
                    Label = new LanguageMap("en", "test"),
                }
            }
        };

        var expected = new AuthProbeService2
        {
            Id = "http://example/auth2",
            Service = new List<IService>
            {
                new AuthAccessService2 { Id = "http://example/access2", },
                new AuthAccessService2 { Id = "http://example/access2/again", }
            }
        };
        
        // Act
        var actual = authProbeService.ToEmbeddedService();
        
        // Assert
        actual.Should().BeEquivalentTo(expected);
        actual.Context.Should().BeNull();
    }
}