using System.Collections.Generic;
using System.Linq;
using DLCS.Web.Response;
using FakeItEasy;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Orchestrator.Features.Auth;
using Orchestrator.Settings;
using Xunit;

namespace Orchestrator.Tests.Features.Auth
{
    public class AuthCookieManagerTests
    {
        [Fact]
        public void GetAuthCookieKey_ReturnsExpected()
        {
            // Arrange
            const string cookieNameFormat = "id-{0}";
            const int customer = 99;
            const string expected = "id-99";

            var sut = GetSut();

            // Act
            var actual = sut.GetAuthCookieKey(cookieNameFormat, customer);

            // Assert
            actual.Should().Be(expected);
        }
        
        [Fact]
        public void GetCookieValueForId_ReturnsExpected()
        {
            // Arrange
            const string cookieId = "1212121212";
            const string expected = "id=1212121212";
            
            var sut = GetSut();

            // Act
            var actual = sut.GetCookieValueForId(cookieId);

            // Assert
            actual.Should().Be(expected);
        }
        
        [Fact]
        public void GetCookieIdFromValue_ReturnsExpected()
        {
            // Arrange
            const string cookieValue = "id=1212121212";
            const string expected = "1212121212";
            
            var sut = GetSut();

            // Act
            var actual = sut.GetCookieIdFromValue(cookieValue);

            // Assert
            actual.Should().Be(expected);
        }
        
        public AuthCookieManager GetSut(bool useCurrentDomainForCookie = true, params string[] additionalDomains)
        {
            var context = new DefaultHttpContext();
            var request = context.Request;
            var contextAccessor = A.Fake<IHttpContextAccessor>();
            A.CallTo(() => contextAccessor.HttpContext).Returns(context);
            request.Host = new HostString("http://test.host");
            request.Scheme = "https";

            var options = Options.Create(new AuthSettings()
            {
                CookieDomains = additionalDomains.ToList(),
                CookieNameFormat = "auth-token-{0}",
                UseCurrentDomainForCookie = useCurrentDomainForCookie
            });
            return new AuthCookieManager(contextAccessor, options);
        }
    }
}