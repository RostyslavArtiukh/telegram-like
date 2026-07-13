using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using TelegramLike.Identity.Tests.Api.Harness;

namespace TelegramLike.Identity.Tests.Api;

/// <summary>
/// Auth guard for the Identity service: the <c>/users</c> lookups carry <c>[Authorize]</c>
/// and must reject a request with no/invalid token, while the <c>/auth</c> bootstrap
/// endpoints stay anonymous (a missing token there must NOT be a 401 — those flows mint
/// the first credential).
/// </summary>
public sealed class IdentityAuthTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private readonly HttpClient _anon = factory.CreateClient();

    [Theory]
    [InlineData("GET",  "/users/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/users/by-ids")]
    [InlineData("GET",  "/users/by-username?u=someuser")]
    public async Task AnonymousRequest_ToProtectedEndpoint_Returns401(string method, string path)
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

        var response = await client.GetAsync("/users/00000000-0000-0000-0000-000000000001");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnonymousRequest_ToAuthEndpoint_IsNotUnauthorized()
    {
        // The /auth endpoints are [AllowAnonymous]; without a token the request must reach
        // the action (which then fails on the mocked mediator), never a 401 auth challenge.
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = new StringContent(
                """{"email":"user@example.com","password":"secret123"}""",
                Encoding.UTF8, "application/json")
        };

        var response = await _anon.SendAsync(request);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }
}
