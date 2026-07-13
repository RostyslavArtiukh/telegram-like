using System.Net;
using System.Text;
using FluentAssertions;
using TelegramLike.Client.Auth;
using TelegramLike.Client.Chats;

namespace TelegramLike.Client.Tests.Chats;

/// <summary>
/// The typed Chats client: it emits service-relative paths (the ServicePrefixHandler adds the
/// gateway prefix separately), attaches the Bearer token from the <see cref="IAccessTokenProvider"/>,
/// tags creates with an Idempotency-Key, and deserializes the wire DTOs (enums arrive as strings).
/// </summary>
public sealed class ChatsApiClientTests
{
    private sealed class StubHandler(HttpStatusCode status, string? json = null) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);

            var response = new HttpResponseMessage(status);
            if (json is not null)
                response.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return response;
        }
    }

    private sealed class StubTokenProvider(string? token) : IAccessTokenProvider
    {
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(token);
    }

    private static ChatsApiClient Build(StubHandler handler, string? token = "jwt-abc")
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://gw:8090") };
        return new ChatsApiClient(http, new StubTokenProvider(token));
    }

    [Fact]
    public async Task GetMyChatsAsync_HitsMyPath_AttachesBearer_AndDeserializesEnums()
    {
        const string json =
            """[{"chatId":"11111111-1111-1111-1111-111111111111","type":"Group","name":"G","myRole":"Owner","activeMemberCount":2}]""";
        var handler = new StubHandler(HttpStatusCode.OK, json);
        var client = Build(handler, token: "jwt-abc");

        var chats = await client.GetMyChatsAsync(Guid.NewGuid());

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/chats/my");
        handler.LastRequest!.Headers.Authorization!.ToString().Should().Be("Bearer jwt-abc");

        chats.Should().ContainSingle();
        chats[0].Type.Should().Be(ChatType.Group);
        chats[0].MyRole.Should().Be(MemberRole.Owner);
        chats[0].ActiveMemberCount.Should().Be(2);
    }

    [Fact]
    public async Task GetChatByIdAsync_WhenNotFound_ReturnsNull()
    {
        var handler = new StubHandler(HttpStatusCode.NotFound);
        var client = Build(handler);

        var result = await client.GetChatByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateGroupChatAsync_PostsWithIdempotencyKey_AndReturnsServerId()
    {
        var serverId = Guid.NewGuid();
        var handler = new StubHandler(HttpStatusCode.Created, $$"""{"chatId":"{{serverId}}"}""");
        var client = Build(handler);

        var id = await client.CreateGroupChatAsync(Guid.NewGuid(), "My Group");

        id.Should().Be(serverId);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/chats/group");
        handler.LastRequest!.Headers.Should().ContainKey("Idempotency-Key");
        handler.LastBody.Should().Contain("My Group");
    }

    [Fact]
    public async Task NoToken_OmitsAuthorizationHeader()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "[]");
        var client = Build(handler, token: null);

        await client.GetMyChatsAsync(Guid.NewGuid());

        handler.LastRequest!.Headers.Authorization.Should().BeNull();
    }
}
