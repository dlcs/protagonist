using DLCS.Core.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace API.Auth;

public class JwtAuthHelper
{
    public JwtAuthHelper(IOptions<DlcsSettings> dlcsSettings)
    {
        SigningCredentials = dlcsSettings.Value.JwtKey is {Length: > 0} key
            ? new(
                new SymmetricSecurityKey(Convert.FromBase64String(key)),
                SecurityAlgorithms.HmacSha256Signature)
            : null;
        ValidIssuers = dlcsSettings.Value.JwtValidIssuers.ToArray();
    }

    public SigningCredentials? SigningCredentials { get; }

    public string[] ValidIssuers { get; }
}