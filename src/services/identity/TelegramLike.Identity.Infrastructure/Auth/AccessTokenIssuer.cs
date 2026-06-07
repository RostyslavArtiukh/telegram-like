using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TelegramLike.Identity.Application.Common.Interfaces;

namespace TelegramLike.Identity.Infrastructure.Auth;

/// <summary>
/// Identity is the IdP: it signs the short-lived HMAC-SHA256 access tokens that
/// every downstream service validates (issuer = telegramlike-identity). Adapted
/// from the Web BFF's former ServiceTokenIssuer — the difference is the issuer
/// claim and that the signing now lives here rather than in the gateway.
/// </summary>
internal sealed class AccessTokenIssuer : IAccessTokenIssuer
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _lifetimeSeconds;
    private readonly SigningCredentials _credentials;
    private readonly JwtSecurityTokenHandler _handler = new();

    public AccessTokenIssuer(string secret, string issuer, string audience, int lifetimeSeconds)
    {
        _issuer = issuer;
        _audience = audience;
        _lifetimeSeconds = lifetimeSeconds;
        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256);
    }

    public AccessToken IssueForUser(Guid userId)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            },
            notBefore: now,
            expires: now.AddSeconds(_lifetimeSeconds),
            signingCredentials: _credentials);

        return new AccessToken(_handler.WriteToken(token), _lifetimeSeconds);
    }
}
