using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DLCS.Web.Response;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace DLCS.Web.Tests.Response
{
    public class HttpResponseMessageXTests
    {
        [Fact]
        public async Task ReadAsJsonAsync_Throws_IfNon2xx_AndEnsureStatusTrue()
        {
            // Arrange
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest
            };

            Func<Task> action = async () => await response.ReadAsJsonAsync<DateTime>();

            // Assert
            await action.Should().ThrowAsync<HttpRequestException>();
        }
        
        [Fact]
        public async Task ReadAsJsonAsync_ReturnsNull_IfNotJson()
        {
            // Arrange
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("foo", Encoding.UTF8, "application/xml")
            };

            // Act
            var json = await response.ReadAsJsonAsync<NewtonTest>();

            // Assert
            json.Should().BeNull();
        }
        
        [Fact]
        public async Task ReadAsJsonAsync_ReturnsJson_ViaNewtonsoft()
        {
            // Arrange
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"bar\":\"baz\", \"test\": \"result\"}", Encoding.UTF8,
                    "application/json")
            };

            var expected = new NewtonTest
            {
                Foo = "baz",
                Test = "result"
            };

            // Act
            var json = await response.ReadAsJsonAsync<NewtonTest>();
            

            // Assert
            json.Should().BeEquivalentTo(expected);
        }
        
        [Theory]
        [InlineData("application/json")]
        [InlineData("application/ld+json")]
        public void IsJsonResponse_True_IfResponseContainsJson(string mediaType)
        {
            // Arrange
            var response = new HttpResponseMessage
            {
                Content = new StringContent("foo", Encoding.UTF8, mediaType)
            };

            // Assert
            response.IsJsonResponse().Should().BeTrue();
        }
        
        [Theory]
        [InlineData("application/xml")]
        [InlineData("text/plain")]
        public void IsJsonResponse_Fale_IfResponseNotJson(string mediaType)
        {
            // Arrange
            var response = new HttpResponseMessage
            {
                Content = new StringContent("foo", Encoding.UTF8, mediaType)
            };

            // Assert
            response.IsJsonResponse().Should().BeFalse();
        }

        public class NewtonTest
        {
            // JsonProperty validates Newtonsoft is used
            [JsonProperty("bar")]
            public string Foo { get; set; }
            
            public string Test { get; set; }
        }
    }
}