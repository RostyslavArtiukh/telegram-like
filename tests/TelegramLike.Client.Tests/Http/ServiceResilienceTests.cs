using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TelegramLike.Client.Http;

namespace TelegramLike.Client.Tests.Http;

/// <summary>
/// The shared resilience pipeline retries transient failures for safe/idempotent requests
/// but NOT for a bare POST/PATCH (so a lost response never double-sends) — UNLESS the request
/// carries an <c>Idempotency-Key</c>, meaning the server dedupes it and retrying is safe.
/// These assert the custom <c>Retry.ShouldHandle</c> predicate by counting handler invocations.
/// </summary>
public sealed class ServiceResilienceTests
{
    // MaxRetryAttempts = 3 → at most 4 total invocations (initial + 3 retries).
    private const int MaxAttempts = 4;

    private sealed class CountingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private static (HttpClient Client, CountingHandler Handler) Build(HttpStatusCode status)
    {
        var handler = new CountingHandler(status);
        var services = new ServiceCollection();
        services.AddHttpClient("t", c => c.BaseAddress = new Uri("http://gw"))
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddServiceResilience();

        var client = services.BuildServiceProvider()
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient("t");

        return (client, handler);
    }

    [Fact]
    public async Task TransientFailure_OnGet_IsRetried()
    {
        var (client, handler) = Build(HttpStatusCode.ServiceUnavailable);

        var response = await client.GetAsync("/x");

        handler.Calls.Should().Be(MaxAttempts);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Success_OnGet_IsNotRetried()
    {
        var (client, handler) = Build(HttpStatusCode.OK);

        await client.GetAsync("/x");

        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task TransientFailure_OnPostWithoutIdempotencyKey_IsNotRetried()
    {
        var (client, handler) = Build(HttpStatusCode.ServiceUnavailable);

        await client.PostAsync("/x", new StringContent(""));

        handler.Calls.Should().Be(1, "a bare POST must not be retried — a lost response could double-send");
    }

    [Fact]
    public async Task TransientFailure_OnPostWithIdempotencyKey_IsRetried()
    {
        var (client, handler) = Build(HttpStatusCode.ServiceUnavailable);

        var request = new HttpRequestMessage(HttpMethod.Post, "/x");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("");

        await client.SendAsync(request);

        handler.Calls.Should().Be(MaxAttempts, "an Idempotency-Key means the server dedupes, so retrying is safe");
    }
}
