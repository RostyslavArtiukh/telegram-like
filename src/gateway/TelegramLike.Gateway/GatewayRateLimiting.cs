using System.Buffers.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

namespace TelegramLike.Gateway;

/// <summary>
/// Per-caller rate limiting at the front door. Nothing anywhere used to bound how fast one
/// client could call, so a single authenticated client in a loop could saturate Messaging and
/// — through fan-out — every consumer downstream of it ([TL-128]).
/// </summary>
/// <remarks>
/// Buckets are per caller, not per instance, because the interesting limit is "one user" and
/// browser users all arrive from the <b>same address</b>: the Web BFF proxies them. So an
/// authenticated request is bucketed by the <c>sub</c> of its bearer token.
/// <para>
/// That token is <b>read, not verified</b> — the gateway is deliberately not a trust boundary
/// (each service validates the JWT itself), and this is a bucket key, never an authorization
/// decision. A caller who fabricates <c>sub</c> values can spread themselves across buckets;
/// what they buy is the right to have unsigned requests rejected downstream slightly faster,
/// since nothing they send will pass a service's validation.
/// </para>
/// <para>
/// Requests with no bearer token — sign-in, registration — fall back to the source address,
/// which for browser traffic is the BFF. That bucket is therefore an <b>aggregate</b> cap on
/// unauthenticated traffic rather than a per-user one, and is sized accordingly.
/// </para>
/// </remarks>
internal static class GatewayRateLimiting
{
    // Probes and scrapes must never be throttled: a limiter that starves the healthcheck
    // takes the instance out of the load balancer for being busy, which is backwards.
    private static readonly string[] ExemptPrefixes = ["/health", "/metrics"];

    public static IServiceCollection AddGatewayRateLimiting(
        this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("RateLimiting");
        if (!section.GetValue("Enabled", true)) return services;

        var user = Budget(section.GetSection("User"), burst: 300, perSecond: 15);
        var anonymous = Budget(section.GetSection("Anonymous"), burst: 120, perSecond: 20);

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (IsExempt(context.Request.Path)) return RateLimitPartition.GetNoLimiter("exempt");

                var caller = CallerKey(context);
                var budget = caller.StartsWith("user:", StringComparison.Ordinal) ? user : anonymous;

                return RateLimitPartition.GetTokenBucketLimiter(caller, _ => budget);
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                // Without this a client that retries blindly makes the overload worse. The
                // bucket replenishes every second, so that is the honest answer.
                context.HttpContext.Response.Headers.RetryAfter = "1";

                await context.HttpContext.Response.WriteAsync(
                    "Too many requests. Slow down and retry.", cancellationToken);
            };
        });

        return services;
    }

    private static TokenBucketRateLimiterOptions Budget(IConfiguration section, int burst, int perSecond) => new()
    {
        // Burst is what a page load costs — several calls at once is normal and must not trip.
        TokenLimit = section.GetValue("Burst", burst),
        TokensPerPeriod = section.GetValue("PerSecond", perSecond),
        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        AutoReplenishment = true,

        // Reject rather than queue. Queuing at the front door just relocates the backlog and
        // holds connections open while it does — the point here is to shed the load.
        QueueLimit = 0,
    };

    internal static bool IsExempt(PathString path) =>
        ExemptPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The bucket this request belongs to: its bearer token's subject, else its source address.
    /// </summary>
    internal static string CallerKey(HttpContext context)
    {
        var subject = BearerSubject(context.Request.Headers.Authorization.ToString());
        if (subject is not null) return $"user:{subject}";

        // One shared bucket for callers we cannot tell apart at all — better than handing each
        // of them an unlimited one.
        var address = context.Connection.RemoteIpAddress?.ToString();
        return address is null ? "anonymous:unknown" : $"anonymous:{address}";
    }

    /// <summary>
    /// Reads <c>sub</c> out of a bearer JWT without validating it. Bucket key only — see the
    /// class remarks for why not verifying here is deliberate rather than an oversight.
    /// </summary>
    internal static string? BearerSubject(string? authorizationHeader)
    {
        if (string.IsNullOrEmpty(authorizationHeader)) return null;
        if (!authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;

        var token = authorizationHeader["Bearer ".Length..].Trim();
        var segments = token.Split('.');
        if (segments.Length != 3) return null;

        try
        {
            var payload = JsonSerializer.Deserialize<JsonElement>(DecodeBase64Url(segments[1]));
            return payload.TryGetProperty("sub", out var sub) && sub.ValueKind == JsonValueKind.String
                ? sub.GetString()
                : null;
        }
        catch
        {
            // Anything unparseable is simply not a token we can bucket by; the address is the
            // fallback, and the request is still rejected downstream on its own merits.
            return null;
        }
    }

    private static byte[] DecodeBase64Url(string segment) => Base64Url.DecodeFromChars(segment);
}
