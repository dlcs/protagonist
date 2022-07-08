using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Test.Helpers.Http
{
    /// <summary>
    /// Controllable HttpMessageHandler for unit testing HttpClient.
    /// </summary>
    public class ControllableHttpMessageHandler : HttpMessageHandler
    {
        private HttpResponseMessage response;
        public List<string> CallsMade { get; }= new List<string>();

        private Action<HttpRequestMessage> Callback { get; set; }

        private Dictionary<string, CallAndResponse> Callbacks { get; } = new();
        private Dictionary<string, Predicate<HttpRequestMessage>> CallbackSelectors { get; } = new();


        /// <summary>
        /// Helper method to generate an HttpResponseMessage object
        /// </summary>
        public HttpResponseMessage GetResponseMessage(string content, HttpStatusCode httpStatusCode)
        {
            var httpContent = new StringContent(content);

            response = new HttpResponseMessage
            {
                StatusCode = httpStatusCode,
                Content = httpContent,
            };
            return response;
        }
        
        /// <summary>
        /// Set a pre-canned response 
        /// </summary>
        public void SetResponse(HttpResponseMessage response) => this.response = response;

        /// <summary>
        /// Register a callback when SendAsync called. Useful for verifying headers etc.
        /// </summary>
        /// <param name="callback">Function to call when SendAsync request made.</param>
        public void RegisterCallback(Action<HttpRequestMessage> callback) => Callback = callback;

        /// <summary>
        /// Register a callback using the request path as a key; when the handler receives that pathAndQuery it
        /// will call this callback.
        /// </summary>
        /// <param name="pathAndQuery"></param>
        /// <param name="callback"></param>
        /// <param name="responseContent"></param>
        /// <param name="responseStatusCode"></param>
        public void RegisterCallback(string pathAndQuery, 
            Action<HttpRequestMessage> callback, string responseContent, HttpStatusCode responseStatusCode)
        {
            Callbacks[pathAndQuery] = new CallAndResponse
            {
                Callback = callback, ResponseContent = responseContent, ResponseStatusCode = responseStatusCode
            };
        }

        /// <summary>
        /// Register a predicate that can test the HttpRequestMessage to pick the appropriate callback.
        /// If the key used to register the callback is the path(andQuery) that the handler is called with,
        /// they will be matched automatically.
        /// But if that's not the case, you need to supply a predicate that returns true if the request message matches.
        /// This can be used to select the callback based on any aspect of the request message.
        /// </summary>
        /// <param name="callbackKey"></param>
        /// <param name="selector"></param>
        public void RegisterCallbackSelector(string callbackKey, Predicate<HttpRequestMessage> selector)
        {
            CallbackSelectors[callbackKey] = selector;
        }
        
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var responseMessage = response;
            CallsMade.Add(request.RequestUri.ToString());
            string pathAndQuery = request.RequestUri.PathAndQuery;
            if (Callbacks.ContainsKey(pathAndQuery))
            {
                // We match a callback based on the incoming request path
                Callbacks[pathAndQuery].Callback?.Invoke(request);
                responseMessage = new HttpResponseMessage
                {
                    StatusCode = Callbacks[pathAndQuery].ResponseStatusCode,
                    Content = new StringContent(Callbacks[pathAndQuery].ResponseContent)
                };
            }
            else if (Callback != null)
            {
                // If a single Callback has been configured for this httpHandler
                Callback?.Invoke(request);
            }
            else
            {
                // See if we can match a callback by testing the request against 
                // each of our registered callback selectors
                foreach (var key in CallbackSelectors.Keys)
                {
                    if (CallbackSelectors[key](request) && Callbacks.ContainsKey(key))
                    {
                        Callbacks[key].Callback?.Invoke(request);
                        responseMessage = new HttpResponseMessage
                        {
                            StatusCode = Callbacks[key].ResponseStatusCode,
                            Content = new StringContent(Callbacks[key].ResponseContent)
                        };
                    }
                }
            }

            var tcs = new TaskCompletionSource<HttpResponseMessage>();
            tcs.SetResult(responseMessage);
            return tcs.Task;
        }

        class CallAndResponse
        {
            public Action<HttpRequestMessage> Callback;
            public string ResponseContent;
            public HttpStatusCode ResponseStatusCode;
        }
    }
}