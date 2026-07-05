using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace TelegramLike.Client.Http;

/// <summary>
/// Shared resilience policy for every call the SDK makes to the gateway.
/// One place so all five services get identical timeout / retry / circuit-breaker
/// behaviour instead of each client re-inventing it.
/// </summary>
internal static class ResilientHttpClientExtensions
{
    public static IHttpClientBuilder AddServiceResilience(this IHttpClientBuilder builder)
    {
        builder.AddStandardResilienceHandler(options =>
        {
            // Per-attempt ceiling: a single downstream hop must not hang the caller's
            // UI. Kept short because these are one-hop calls through the gateway.
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);

            // Overall ceiling across all retries for one logical request.
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);

            // Retry transient failures for idempotent methods. POST/PATCH are excluded
            // by default so a lost response never double-sends — UNLESS the request
            // carries an Idempotency-Key, meaning the server dedupes it (e.g. SendMessage),
            // in which case retrying is safe and desirable.
            options.Retry.ShouldHandle = args =>
            {
                var request = args.Context.GetRequestMessage();
                var isUnsafe = request?.Method == HttpMethod.Post || request?.Method == HttpMethod.Patch;
                var isIdempotent = request?.Headers.Contains("Idempotency-Key") == true;
                if (isUnsafe && !isIdempotent)
                    return PredicateResult.False();

                return new ValueTask<bool>(HttpClientResiliencePredicates.IsTransient(args.Outcome));
            };
            options.Retry.MaxRetryAttempts = 3;
            // Fast backoff (200ms base, exponential + jitter) instead of the 2s default:
            // these hops feed an interactive UI, so a down service must be detected in
            // ~1s — not ~14s — and its failures must land inside the breaker's sampling
            // window so it actually trips.
            options.Retry.Delay = TimeSpan.FromMilliseconds(200);

            // Fail fast when a service is down instead of hammering it every call.
            // Min-throughput dropped from the default 100 to suit this app's low
            // traffic, so the breaker can actually trip. SamplingDuration must
            // stay >= 2 * AttemptTimeout.
            options.CircuitBreaker.MinimumThroughput = 5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
        });

        return builder;
    }
}
