namespace Orchestrator.ReverseProxy
{
    /// <summary>
    /// Record that represents how a request should be proxied
    /// </summary>
    /// <param name="Target">Downstream system to Proxy to</param>
    /// <param name="Path">Path to proxy to, if rewritten</param>
    public record ProxyAction(ProxyTo Target, string? Path = null)
    {
        public bool HasPath => !string.IsNullOrWhiteSpace(Path);
    }
    
    public enum ProxyTo
    {
        Unknown,
        Orchestrator,
        Thumbs,
        ImageServer,
        CachingProxy
    }
}