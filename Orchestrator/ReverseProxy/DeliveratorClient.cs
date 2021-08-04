using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Web.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Orchestrator.ReverseProxy
{
    public interface IDeliveratorClient
    {
        Task<bool> VerifyBearerAuth(AssetId id, string bearerToken);
        Task<bool> VerifyCookieAuth(AssetId id, HttpRequest httpRequest);
    }

    /// <summary>
    /// HttpClient for making back-channel HttpRequests to Deliverator
    /// </summary>
    public class DeliveratorClient : IDeliveratorClient
    {
        private readonly HttpClient httpClient;

        public DeliveratorClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }
        
        public async Task<bool> VerifyBearerAuth(AssetId id, string bearerToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/auth/services/authpasses{id}");
            request.Headers.AddBearerTokenAuth(bearerToken);
            var response = await httpClient.SendAsync(request);

            return response.StatusCode == HttpStatusCode.OK;
        }

        public async Task<bool> VerifyCookieAuth(AssetId id, HttpRequest httpRequest)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/auth/services/authpasses{id}");
            var response = await httpClient.SendAsync(request);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return false;
            }

            // TODO - is this for request or response?
            if (response.Headers.TryGetValues("Set-Cookie", out var setCookieValues))
            {
                httpRequest.Headers.Add("Set-Cookie", new StringValues(setCookieValues.ToArray()));
            }

            return true;
        }
    }
}