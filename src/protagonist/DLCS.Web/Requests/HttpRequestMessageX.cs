using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace DLCS.Web.Requests;

public static class HttpRequestMessageX
{
    private static readonly JsonSerializerOptions Settings = new(JsonSerializerDefaults.Web);
    
    /// <summary>
    /// Update HttpRequestMessage.Content property with serialized Json content.
    /// Serialized with camel-case property names to application/json; charset=utf-8 
    /// </summary>
    /// <param name="request"><see cref="HttpRequestMessage"/> object to set content on.</param>
    /// <param name="content">The object to use as httpContent.</param>
    /// <typeparam name="T">Type of model.</typeparam>
    public static void SetJsonContent<T>(this HttpRequestMessage request, T content)
    {
        var serialized = JsonSerializer.Serialize(content, Settings);
        var requestBody = new StringContent(serialized, Encoding.UTF8, "application/json");
        request.Content = requestBody;
    }
}