using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace TelegramLike.Web.Services.NotificationsApi;

/// Issues short-lived HMAC-signed JWTs that Web (BFF) attaches to outbound
/// service-to-service HTTP calls. Downstream services validate the signature
/// to confirm the caller is the BFF and trust the `sub` claim as the user ID.
internal sealed class ServiceTokenIssuer(IOptions<ServiceAuthOptions> options)
{
    private readonly ServiceAuthOptions _opts = options.Value;
    private readonly SigningCredentials _credentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.JwtSecret)),
        SecurityAlgorithms.HmacSha256);
    private readonly JwtSecurityTokenHandler _handler = new();

    public string IssueForUser(Guid userId)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            },
            notBefore: now,
            expires: now.AddSeconds(_opts.TokenLifetimeSeconds),
            signingCredentials: _credentials);

        return _handler.WriteToken(token);
    }
}
