using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace TelegramLike.Api.ServiceDefaults;

/// <summary>
/// The one JWT-bearer setup every service shares. Each service validates tokens with the same
/// symmetric secret / issuer / audience (Identity is the IdP; it validates the tokens it issues).
/// Keeping this in one place means the auth shape can't silently drift between services.
/// </summary>
public static class ServiceAuthExtensions
{
    public static IServiceCollection AddServiceJwtAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSecret = configuration["ServiceAuth:JwtSecret"]
                        ?? throw new InvalidOperationException("ServiceAuth:JwtSecret is not configured.");
        var jwtIssuer = configuration["ServiceAuth:Issuer"]
                        ?? throw new InvalidOperationException("ServiceAuth:Issuer is not configured.");
        var jwtAudience = configuration["ServiceAuth:Audience"]
                          ?? throw new InvalidOperationException("ServiceAuth:Audience is not configured.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtAudience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });
        services.AddAuthorization();

        return services;
    }
}
