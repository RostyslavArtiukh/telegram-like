using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using TelegramLike.Messaging.Api.Tests.Harness;

namespace TelegramLike.Messaging.Api.Tests;

/// <summary>
/// Auth guard: every protected endpoint must reject requests with no/invalid token.
/// </summary>
public sealed class MessagingAuthTests(MessagingApiFactory factory) : IClassFixture<MessagingApiFactory>
{
    private readonly HttpClient _anon = factory.CreateClient();

    [Theory]
    [InlineData("POST",   "/messages")]
    [InlineData("GET",    "/messages/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST",   "/messages/00000000-0000-0000-0000-000000000001/retract")]
    [InlineData("POST",   "/messages/00000000-0000-0000-0000-000000000001/hide")]
    [InlineData("POST",   "/messages/00000000-0000-0000-0000-000000000001/reactions")]
    [InlineData("DELETE", "/messages/00000000-0000-0000-0000-000000000001/reactions/Like")]
    [InlineData("POST",   "/messages/00000000-0000-0000-0000-000000000001/read")]
    [InlineData("GET",    "/chats/00000000-0000-0000-0000-000000000001/messages")]
    public async Task AnonymousRequest_Returns401(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST")
        {
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
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

        var response = await client.GetAsync("/messages/00000000-0000-0000-0000-000000000001");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
