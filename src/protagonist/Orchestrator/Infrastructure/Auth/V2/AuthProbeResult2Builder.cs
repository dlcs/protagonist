using IIIF.Auth.V2;
using IIIF.Presentation.V3.Strings;

namespace Orchestrator.Infrastructure.Auth.V2;

public static class AuthProbeResult2Builder
{
    public static AuthProbeResult2 MissingCredentials =
        BuildProbeResult(401, "Missing credentials", "Authorising credentials not found");
    public static AuthProbeResult2 UnexpectedError = BuildProbeResult(500, "Unexpected Error", "Unexpected Error");
    public static AuthProbeResult2 Okay => new() { Status = 200 };
    
    public static AuthProbeResult2 BuildProbeResult(int status, string heading, string note)
        => new()
        {
            Status = status,
            Heading = new LanguageMap("en", heading),
            Note = new LanguageMap("en", note),
        };
}