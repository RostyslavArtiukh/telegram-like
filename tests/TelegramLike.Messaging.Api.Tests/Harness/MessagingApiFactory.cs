using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace TelegramLike.Messaging.Api.Tests.Harness;

/// <summary>
/// Shared WebApplicationFactory for Messaging Api integration tests.
/// Replaces IMediator with an NSubstitute mock so no handler or Mongo runs.
/// Provides test JWT config and a real HMAC JWT minter so auth tests exercise
/// the actual JwtBearer pipeline.
/// </summary>
public sealed class MessagingApiFactory : WebApplicationFactory<Program>
{
    public const string TestSecret = "test-jwt-secret-that-is-long-enough-for-hmacsha256";
    public const string TestIssuer = "telegramlike-identity";
    public const string TestAudience = "telegramlike-services";

    /// <summary>Shared NSubstitute mock — tests configure returns/throws on this.</summary>
    public IMediator Mediator { get; } = Substitute.For<IMediator>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ServiceAuth:JwtSecret", TestSecret);
        builder.UseSetting("ServiceAuth:Issuer", TestIssuer);
        builder.UseSetting("ServiceAuth:Audience", TestAudience);
        // Provide stub connection strings so Infrastructure DI doesn't throw.
        builder.UseSetting("MongoDB:ConnectionString", "mongodb://localhost:27017");
        builder.UseSetting("MongoDB:DatabaseName", "test_messaging");
        builder.UseSetting("RabbitMQ:Host", "localhost");

        builder.ConfigureTestServices(services =>
        {
            // Replace the real IMediator with our mock — no handlers execute.
            services.RemoveAll<IMediator>();
            services.AddSingleton(Mediator);
        });
    }

    /// <summary>
    /// Mints a real HMAC-SHA256 JWT that the production JwtBearer pipeline will validate.
    /// </summary>
    public string MintToken(Guid userId, int expiresInSeconds = 300)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())],
            expires: DateTime.UtcNow.AddSeconds(expiresInSeconds),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Creates an HttpClient with a Bearer token for the given userId.</summary>
    public HttpClient CreateAuthenticatedClient(Guid? userId = null)
    {
        var id = userId ?? Guid.NewGuid();
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", MintToken(id));
        return client;
    }
}
