using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using TelegramLike.Notifications.Tests.Api.Harness;

namespace TelegramLike.Notifications.Tests.Api;

/// <summary>
/// Auth guard: every notifications endpoint is <c>[Authorize]</c> — a request with no/invalid
/// token must be rejected with 401 before any handler runs.
/// </summary>
public sealed class NotificationsAuthTests(NotificationsApiFactory factory) : IClassFixture<NotificationsApiFactory>
{
    private readonly HttpClient _anon = factory.CreateClient();

    [Theory]
    [InlineData("GET",  "/notifications")]
    [InlineData("GET",  "/notifications/unread-count")]
    [InlineData("POST", "/notifications/00000000-0000-0000-0000-000000000001/read")]
    [InlineData("POST", "/notifications/read-all")]
    [InlineData("POST", "/notifications/chats/00000000-0000-0000-0000-000000000001/read")]
    public async Task AnonymousRequest_Returns401(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
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

        var response = await client.GetAsync("/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
