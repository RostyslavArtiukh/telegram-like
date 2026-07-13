using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace TelegramLike.Identity.Tests.Api.Harness;

/// <summary>
/// Shared WebApplicationFactory for Identity Api integration tests.
/// Replaces IMediator with an NSubstitute mock so no handler, Mongo or Redis runs
/// (the Mongo/Redis clients are lazy singletons, so stub connection strings suffice).
/// Unlike the Chats/Messaging harnesses this mints no JWTs — the filter tests hit the
/// anonymous /auth endpoints.
/// </summary>
public sealed class IdentityApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Shared NSubstitute mock — tests configure returns/throws on this.</summary>
    public IMediator Mediator { get; } = Substitute.For<IMediator>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ServiceAuth:JwtSecret", "test-jwt-secret-that-is-long-enough-for-hmacsha256");
        builder.UseSetting("ServiceAuth:Issuer", "telegramlike-identity");
        builder.UseSetting("ServiceAuth:Audience", "telegramlike-services");
        // Provide stub connection strings so Infrastructure DI doesn't throw.
        builder.UseSetting("MongoDB:ConnectionString", "mongodb://localhost:27017");
        builder.UseSetting("MongoDB:DatabaseName", "test_identity");
        builder.UseSetting("Redis:ConnectionString", "localhost:6379");

        builder.ConfigureTestServices(services =>
        {
            // Replace the real IMediator with our mock — no handlers execute.
            services.RemoveAll<IMediator>();
            services.AddSingleton(Mediator);

            // Strip hosted services — UserIndexInitializer awaits its Mongo connection
            // in StartAsync (~30s server selection against the stub connection string),
            // which would crash the whole test host. Same pattern as MessagingApiFactory.
            services.RemoveAll<IHostedService>();
        });
    }
}
