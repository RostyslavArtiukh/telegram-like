using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using TelegramLike.Chats.Tests.Api.Harness;

namespace TelegramLike.Chats.Tests.Api;

/// <summary>
/// Auth guard: every protected endpoint must reject requests with no/invalid token.
/// </summary>
public sealed class ChatsAuthTests(ChatsApiFactory factory) : IClassFixture<ChatsApiFactory>
{
    private readonly HttpClient _anon = factory.CreateClient();

    [Theory]
    [InlineData("GET",  "/chats/my")]
    [InlineData("GET",  "/chats/00000000-0000-0000-0000-000000000001")]
    [InlineData("GET",  "/chats/00000000-0000-0000-0000-000000000001/members")]
    [InlineData("POST", "/chats/direct")]
    [InlineData("POST", "/chats/group")]
    [InlineData("POST", "/chats/broadcast")]
    [InlineData("PATCH", "/chats/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/chats/00000000-0000-0000-0000-000000000001/join")]
    [InlineData("POST", "/chats/00000000-0000-0000-0000-000000000001/leave")]
    [InlineData("POST", "/chats/00000000-0000-0000-0000-000000000001/members/00000000-0000-0000-0000-000000000002/kick")]
    [InlineData("POST", "/chats/00000000-0000-0000-0000-000000000001/members/00000000-0000-0000-0000-000000000002/role")]
    [InlineData("POST", "/chats/00000000-0000-0000-0000-000000000001/transfer-ownership")]
    public async Task Anonymous_request_returns_401(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PATCH")
        {
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        }

        var response = await _anon.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invalid_token_returns_401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "totally.invalid.token");

        var response = await client.GetAsync("/chats/my");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
