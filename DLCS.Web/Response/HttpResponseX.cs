using Microsoft.AspNetCore.Http;

namespace DLCS.Web.Response
{
    public static class HttpResponseX
    {
        /// <summary>
        /// Return a seeOther response (HTTP 303) to the client
        /// </summary>
        /// <param name="httpResponse">Current <see cref="HttpResponse"/> object</param>
        /// <param name="location">The URL to redirect the client to. This must be properly encoded for use in http headers
        /// where only ASCII characters are allowed.</param>
        public static void SeeOther(this HttpResponse httpResponse, string location)
        {
            httpResponse.StatusCode = 303;
            httpResponse.Headers["Location"] = location;
        }
    }
}