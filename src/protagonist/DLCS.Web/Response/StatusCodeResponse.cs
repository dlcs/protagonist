using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace DLCS.Web.Response;

public class StatusCodeResponse
{
    public HttpStatusCode StatusCode { get; }
    public string Message { get; }
    
    private readonly JsonSerializerOptions settings = new(JsonSerializerDefaults.Web);

    public StatusCodeResponse(HttpStatusCode statusCode, string message)
    {
        StatusCode = statusCode;
        Message = message;
    }

    /// <summary>
    /// Create a StatusCodeResponse for 404 NotFound.
    /// </summary>
    /// <param name="message">Friendly error message explaining message.</param>
    public static StatusCodeResponse NotFound(string message) => new(HttpStatusCode.NotFound, message);
    
    /// <summary>
    /// Create a StatusCodeResponse for 400 NotFound.
    /// </summary>
    /// <param name="message">Friendly error message explaining message.</param>
    public static StatusCodeResponse BadRequest(string message) => new(HttpStatusCode.BadRequest, message);

    /// <summary>
    /// Set response StatusCode and write a JSON representation of object to HttpResponse.
    /// </summary>
    /// <param name="response"></param>
    public async Task WriteJsonResponse(HttpResponse response)
    {
        response.ContentType = "application/json";
        response.StatusCode = (int)StatusCode;
        await JsonSerializer.SerializeAsync(response.Body, this, settings);
    }
}