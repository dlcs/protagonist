using System.Net;
using DLCS.AWS.Transcoding.Models.Request;

namespace DLCS.AWS.Tests.Transcoding.Models.Request;

public class CreateJobResponseTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Accepted)]
    [InlineData(HttpStatusCode.Created)]
    [InlineData(HttpStatusCode.NoContent)]
    public void Success_True(HttpStatusCode statusCode)
        => new CreateJobResponse("test2", statusCode).Success.Should().BeTrue();
    
    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.Redirect)]
    public void Success_False(HttpStatusCode statusCode)
        => new CreateJobResponse("test2", statusCode).Success.Should().BeFalse();
}
