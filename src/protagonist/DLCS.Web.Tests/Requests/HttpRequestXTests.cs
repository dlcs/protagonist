using DLCS.Web.Requests;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DLCS.Web.Tests.Requests;

public class HttpRequestXTests
{
    [Fact]
    public void GetFirstQueryParamValue_Finds_Single_Value()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.QueryString = new QueryString("?test=aaa&test2=bbb");
        
        // act
        var test = httpRequest.GetFirstQueryParamValue("test");
        
        // assert
        test.Should().Be("aaa");
    }
    
    
    [Fact]
    public void GetFirstQueryParamValue_Ignores_Further_Values()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.QueryString = new QueryString("?test=aaa&test=bbb&test2=ccc");
        
        // act
        var test = httpRequest.GetFirstQueryParamValue("test");
        
        // assert
        test.Should().Be("aaa");
    }
    
    
    [Fact]
    public void GetFirstQueryParamValueAsInt_Finds_Int()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.QueryString = new QueryString("?test=12");
        
        // act
        var test = httpRequest.GetFirstQueryParamValueAsInt("test");
        
        // assert
        test.Should().Be(12);
    }
}