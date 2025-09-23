using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Test.Helpers.Http;

/// <summary>
/// Controllable HttpMessageHandler for unit testing HttpClient.
/// </summary>
public class ControllableHttpMessageHandler : HttpMessageHandler
{
    private static readonly JsonSerializerOptions Settings = new(JsonSerializerDefaults.Web);
    private HttpResponseMessage response;
    public List<string> CallsMade { get; }= new();

    private Action<HttpRequestMessage> Callback { get; set; }

    private Dictionary<string, CallAndResponse> Callbacks { get; } = new();
    private Dictionary<string, Predicate<HttpRequestMessage>> CallbackSelectors { get; } = new();
    
    /// <summary>
    /// Helper method to generate an HttpResponseMessage object
    /// </summary>
    public HttpResponseMessage GetJsonResponseMessage<T>(T content, HttpStatusCode httpStatusCode)
    {
        var serialized = JsonSerializer.Serialize(content, Settings);
        var httpContent = new StringContent(serialized, Encoding.UTF8, "application/json");

        response = new HttpResponseMessage
        {
            StatusCode = httpStatusCode,
            Content = httpContent,
        };
        return response;
    }

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
    public void SetResponse(HttpResponseMessage responseMessage) => response = responseMessage;

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

    /// <summary>
    /// Combines the two operations above for simpler Unit Tests
    /// </summary>
    /// <param name="callbackKey">A string that uniquely identifies this callback</param>
    /// <param name="callback">The action to invoke when httpHandler handles a message</param>
    /// <param name="selector">A predicate that matches the HttpRequestMessage, so the handler can find _this_ callback</param>
    /// <param name="responseContent">The response body the handler should return</param>
    /// <param name="responseStatusCode">The status code the handler should return</param>
    public void RegisterCallbackWithSelector(
        string callbackKey,
        Action<HttpRequestMessage> callback,
        Predicate<HttpRequestMessage> selector,
        string responseContent, HttpStatusCode responseStatusCode)
    {
        RegisterCallback(callbackKey, callback, responseContent, responseStatusCode);
        RegisterCallbackSelector(callbackKey, selector);
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
