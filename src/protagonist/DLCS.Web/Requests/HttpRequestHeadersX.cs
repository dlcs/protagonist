using System;
using System.Net.Http.Headers;
using System.Text;
using DLCS.Core.Guard;

namespace DLCS.Web.Requests
{
    public static class HttpRequestHeadersX
    {
        /// <summary>
        /// Add Basic auth header to <see cref="HttpRequestHeaders"/> object.
        /// </summary>
        /// <param name="headers"><see cref="HttpRequestHeaders"/> object to add headers to.</param>
        /// <param name="key">Key/Username for basic auth.</param>
        /// <param name="secret">Secret/Password for basic auth.</param>
        public static void AddBasicAuth(this HttpRequestHeaders headers, string key, string secret)
        {
            key.ThrowIfNullOrEmpty(nameof(key));
            secret.ThrowIfNullOrEmpty(nameof(secret));

            var creds = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{key}:{secret}"));
            headers.AddBasicAuth(creds);
        }
        
        /// <summary>
        /// Add Basic auth header to <see cref="HttpRequestHeaders"/> object.
        /// </summary>
        /// <param name="headers"><see cref="HttpRequestHeaders"/> object to add headers to.</param>
        /// <param name="base64Encoded">uname:pword as base64 encoded string.</param>
        public static void AddBasicAuth(this HttpRequestHeaders headers, string base64Encoded)
        {
            headers.ThrowIfNull(nameof(headers));
            base64Encoded.ThrowIfNull(nameof(base64Encoded));
            
            headers.Authorization = new AuthenticationHeaderValue("Basic", base64Encoded);
        }
        
        /// <summary>
        /// Add Bearer auth header to <see cref="HttpRequestHeaders"/> object.
        /// </summary>
        /// <param name="headers"><see cref="HttpRequestHeaders"/> object to add headers to.</param>
        /// <param name="token">Bearer token.</param>
        public static void AddBearerTokenAuth(this HttpRequestHeaders headers, string token)
        {
            headers.ThrowIfNull(nameof(headers));
            token.ThrowIfNull(nameof(token));
            
            headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}