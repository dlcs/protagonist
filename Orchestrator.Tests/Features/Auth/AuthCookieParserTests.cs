using FluentAssertions;
using Orchestrator.Features.Auth;
using Xunit;

namespace Orchestrator.Tests.Features.Auth
{
    public class AuthCookieParserTests
    {
        [Fact]
        public void GetAuthCookieKey_ReturnsExpected()
        {
            const string cookieNameFormat = "id-{0}";
            const int customer = 99;
            const string expected = "id-99";

            var actual = AuthCookieParser.GetAuthCookieKey(cookieNameFormat, customer);

            actual.Should().Be(expected);
        }
        
        [Fact]
        public void GetCookieValueForId_ReturnsExpected()
        {
            const string cookieId = "1212121212";
            const string expected = "id=1212121212";

            var actual = AuthCookieParser.GetCookieValueForId(cookieId);

            actual.Should().Be(expected);
        }
        
        [Fact]
        public void GetCookieIdFromValue_ReturnsExpected()
        {
            const string cookieValue = "id=1212121212";
            const string expected = "1212121212";

            var actual = AuthCookieParser.GetCookieIdFromValue(cookieValue);

            actual.Should().Be(expected);
        }
    }
}