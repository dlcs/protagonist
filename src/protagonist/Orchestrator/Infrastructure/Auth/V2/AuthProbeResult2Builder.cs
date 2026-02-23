using IIIF.Auth.V2;
using IIIF.Presentation.V3.Strings;

namespace Orchestrator.Infrastructure.Auth.V2;

public static class AuthProbeResult2Builder
{
    /// <summary>
    /// Returns 401 response with "Missing Credentials" message
    /// </summary>
    public static readonly AuthProbeResult2 MissingCredentials =
        BuildProbeResult(401, "Missing credentials", "Authorising credentials not found");

    /// <summary>
    /// Returns 401 response with "Unobtainable Role" message
    /// </summary>
    public static readonly AuthProbeResult2 UnobtainableRole =
        BuildProbeResult(401, "Unobtainable Role", "Asset role is unobtainable");

    /// <summary>
    /// Returns 500 response with "Unexpected Error" message
    /// </summary>
    public static readonly AuthProbeResult2 UnexpectedError =
        BuildProbeResult(500, "Unexpected Error", "Unexpected Error");
    
    /// <summary>
    /// Returns empty 200 response without heading or note
    /// </summary>
    public static readonly AuthProbeResult2 Okay = new() { Status = 200 };

    private static AuthProbeResult2 BuildProbeResult(int status, string heading, string note)
        => new()
        {
            Status = status,
            Heading = new LanguageMap("en", heading),
            Note = new LanguageMap("en", note),
        };
}
