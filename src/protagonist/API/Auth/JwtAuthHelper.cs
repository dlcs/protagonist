using DLCS.Core.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace API.Auth;

public class JwtAuthHelper(IOptions<DlcsSettings> dlcsSettings)
{
    public SigningCredentials? SigningCredentials { get; } = dlcsSettings.Value.JwtKey is {Length: > 0} key
        ? new(
            new SymmetricSecurityKey(Convert.FromBase64String(key)),
            SecurityAlgorithms.HmacSha256Signature)
        : null;

    public string[] ValidIssuers { get; } = dlcsSettings.Value.JwtValidIssuers.ToArray();
}