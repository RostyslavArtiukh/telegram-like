using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using TelegramLike.Presence.Tests.Api.Harness;

namespace TelegramLike.Presence.Tests.Api;

/// <summary>
/// Auth guard: every presence and typing endpoint is <c>[Authorize]</c> — a request with
/// no/invalid token must be rejected with 401 before any handler runs.
/// </summary>
public sealed class PresenceAuthTests(PresenceApiFactory factory) : IClassFixture<PresenceApiFactory>
{
    private readonly HttpClient _anon = factory.CreateClient();

    [Theory]
    [InlineData("POST", "/presence/heartbeat")]
    [InlineData("POST", "/presence/offline")]
    [InlineData("GET",  "/presence/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/presence/batch")]
    [InlineData("POST", "/presence/typing/00000000-0000-0000-0000-000000000001/start")]
    [InlineData("POST", "/presence/typing/00000000-0000-0000-0000-000000000001/stop")]
    [InlineData("GET",  "/presence/typing/00000000-0000-0000-0000-000000000001")]
    public async Task AnonymousRequest_Returns401(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
        {
            request.Content = new StringContent("[]", Encoding.UTF8, "application/json");
        }

        var response = await _anon.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InvalidToken_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "totally.invalid.token");

        var response = await client.PostAsync("/presence/heartbeat", new StringContent(""));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
