namespace Orchestrator.Infrastructure.NamedQueries.Requests
{
    /// <summary>
    /// A marker interface containing the minimum fields required to satisfy a named query request.
    /// </summary>
    public interface IBaseNamedQueryRequest
    {
        public string CustomerPathValue { get; }
        
        public string NamedQuery { get; }
        
        public string? NamedQueryArgs { get; }
    }
}