using System.Net;

namespace DLCS.Core.Tests;

public class HttpStatusCodeXTests
{
    [Theory]
    [InlineData(HttpStatusCode.Accepted, true)]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.NoContent, true)]
    [InlineData(HttpStatusCode.Redirect, false)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.BadGateway, false)]
    [InlineData(HttpStatusCode.InsufficientStorage, false)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public void IsSuccess_Correct(HttpStatusCode statusCode, bool isSuccess)
        => statusCode.IsSuccess().Should().Be(isSuccess);
    
    [Theory]
    [InlineData(HttpStatusCode.OK, false)]
    [InlineData(HttpStatusCode.Redirect, false)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.BadGateway, true)]
    [InlineData(HttpStatusCode.InsufficientStorage, true)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    public void IsServerError_Correct(HttpStatusCode statusCode, bool isSuccess)
        => statusCode.IsServerError().Should().Be(isSuccess);
}
