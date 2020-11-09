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
            headers.ThrowIfNull(nameof(headers));
            key.ThrowIfNullOrEmpty(nameof(key));
            secret.ThrowIfNullOrEmpty(nameof(secret));

            var creds = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{key}:{secret}"));
            headers.Authorization = new AuthenticationHeaderValue("Basic", creds);
        }
    }
}