using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace DLCS.Web.Response
{
    public class StatusCodeResponse
    {
        public HttpStatusCode StatusCode { get; }
        public string Message { get; }

        public StatusCodeResponse(HttpStatusCode statusCode, string message)
        {
            StatusCode = statusCode;
            Message = message;
        }

        /// <summary>
        /// Create a StatusCodeResponse for 404 NotFound.
        /// </summary>
        /// <param name="message">Friendly error message explaining message.</param>
        public static StatusCodeResponse NotFound(string message) => new StatusCodeResponse(HttpStatusCode.NotFound, message);

        /// <summary>
        /// Set response StatusCode and write a JSON representation of object to HttpResponse.
        /// </summary>
        /// <param name="response"></param>
        public Task WriteJsonResponse(HttpResponse response)
        {
            response.ContentType = "application/json";
            response.StatusCode = (int)StatusCode;
            return response.WriteAsync(this.ToString());
        }

        public override string ToString() => JsonConvert.SerializeObject(this);
    }
}