namespace Orchestrator.Features.Auth.Paths;

public interface IAuthPathGenerator
{
    /// <summary>
    /// Generate full auth path using specified params for template replacement
    /// </summary>
    string GetAuthPathForRequest(string customer, string behaviour);
}