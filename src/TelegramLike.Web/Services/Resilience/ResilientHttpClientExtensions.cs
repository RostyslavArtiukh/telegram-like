using Microsoft.Extensions.Http.Resilience;

namespace TelegramLike.Web.Services.Resilience;

/// <summary>
/// Shared resilience policy for every downstream service call the BFF makes.
/// One place so all five services get identical timeout / retry / circuit-breaker
/// behaviour instead of each client re-inventing it.
/// </summary>
internal static class ResilientHttpClientExtensions
{
    public static IHttpClientBuilder AddServiceResilience(this IHttpClientBuilder builder)
    {
        builder.AddStandardResilienceHandler(options =>
        {
            // Per-attempt ceiling: a single downstream hop must not hang the Blazor
            // circuit. Kept short because these are intra-cluster HTTP calls.
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);

            // Overall ceiling across all retries for one logical request.
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);

            // Retry transient failures — but only for idempotent methods. POST/PATCH
            // are excluded so a lost response never double-sends a message,
            // double-creates a chat, or double-registers a user.
            options.Retry.DisableForUnsafeHttpMethods();
            options.Retry.MaxRetryAttempts = 3;
            // Fast backoff (200ms base, exponential + jitter) instead of the 2s default:
            // these are intra-cluster hops feeding an interactive UI, so a down service
            // must be detected in ~1s — not ~14s — and its failures must land inside the
            // breaker's sampling window so it actually trips.
            options.Retry.Delay = TimeSpan.FromMilliseconds(200);

            // Fail fast when a service is down instead of hammering it every call.
            // Min-throughput dropped from the default 100 to suit this app's low
            // local traffic, so the breaker can actually trip. SamplingDuration must
            // stay >= 2 * AttemptTimeout.
            options.CircuitBreaker.MinimumThroughput = 5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
        });

        return builder;
    }
}
