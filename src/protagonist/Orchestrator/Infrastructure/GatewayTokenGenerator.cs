using System;
using System.Security.Cryptography;
using System.Text;
using DLCS.Core.Strings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure;

/// <summary>
/// Generates the HMAC signature that proves to the downstream image-server that a request came from Orchestrator.
/// </summary>
/// <remarks>
/// The signed value is "orch|v1|{bucket}|{identifier}" where {bucket} is a sliding time-bucket and {identifier} is the
/// IIIF ImageApi identifier being requested.
/// </remarks>
public class GatewayTokenGenerator
{
    /// <summary>
    /// Header that generated token is sent in
    /// </summary>
    public const string TokenHeader = "x-gateway-token";

    private const string SignaturePrefix = "orch|v1";

    private readonly byte[] secret;
    private readonly long windowSecs;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Whether request signing is configured. If false <see cref="GetToken"/> always returns null.
    /// </summary>
    public bool IsEnabled { get; }

    public GatewayTokenGenerator(IOptions<OrchestratorSettings> orchestratorSettings,
        ILogger<GatewayTokenGenerator> logger,
        TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
        var settings = orchestratorSettings.Value.GatewayToken;
        IsEnabled = settings.Secret.HasText();

        if (!IsEnabled)
        {
            logger.LogWarning("GatewayTokens disabled, no secret provided");
            secret = [];
            return;
        }

        if (settings.WindowSecs <= 0)
        {
            throw new ArgumentException(
                $"GatewayToken:WindowSecs must be greater than 0 but is {settings.WindowSecs}",
                nameof(orchestratorSettings));
        }

        secret = Encoding.UTF8.GetBytes(settings.Secret!);
        windowSecs = settings.WindowSecs;
    }

    /// <summary>
    /// Generate signature for given IIIF ImageApi identifier.
    /// </summary>
    /// <param name="identifier">
    /// The {identifier} for the asset, exactly as it appears in the path sent to the image-server (ie url-encoded)
    /// </param>
    /// <returns>Lowercase hex encoded signature, or null if signing is not configured</returns>
    public string? GetToken(string identifier)
    {
        if (!IsEnabled) return null;

        var bucket = timeProvider.GetUtcNow().ToUnixTimeSeconds() / windowSecs;
        var signedValue = $"{SignaturePrefix}|{bucket}|{identifier}";
        var hash = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(signedValue));

        // lower invariant, avoids issues of upper/lower case diff in generation + validation side
        return Convert.ToHexStringLower(hash);
    }
}
