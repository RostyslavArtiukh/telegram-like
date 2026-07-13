using System.Net;
using FluentAssertions;
using TelegramLike.Client.Http;

namespace TelegramLike.Client.Tests.Http;

/// <summary>
/// The handler prepends a service's gateway prefix to the outgoing request path so all
/// typed clients can share one gateway base address. Clients keep service-relative paths;
/// the prefix is added here, and the YARP gateway strips it. For a service whose route
/// prefix equals its own path prefix (chats/notifications/presence) the wire path is
/// deliberately doubled (e.g. /chats/my → /chats/chats/my) then stripped once at the gateway.
/// </summary>
public sealed class ServicePrefixHandlerTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static (HttpClient Client, CapturingHandler Capture) Build(string prefix)
    {
        var capture = new CapturingHandler();
        var handler = new ServicePrefixHandler(prefix) { InnerHandler = capture };
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://gw:8090") };
        return (client, capture);
    }

    [Fact]
    public async Task PrependsPrefixToPath()
    {
        var (client, capture) = Build("/messaging");

        await client.GetAsync("/messages/123");

        capture.LastUri!.AbsolutePath.Should().Be("/messaging/messages/123");
    }

    [Fact]
    public async Task PreservesQueryString()
    {
        var (client, capture) = Build("/messaging");

        await client.GetAsync("/chats/abc/messages?pageSize=50&before=x");

        capture.LastUri!.AbsolutePath.Should().Be("/messaging/chats/abc/messages");
        capture.LastUri!.Query.Should().Be("?pageSize=50&before=x");
    }

    [Fact]
    public async Task DoublesPrefixForServiceWhoseRoutePrefixMatchesItsOwnPath()
    {
        var (client, capture) = Build("/chats");

        await client.GetAsync("/chats/my");

        capture.LastUri!.AbsolutePath.Should().Be("/chats/chats/my");
    }

    [Fact]
    public async Task PreservesSchemeHostAndPort()
    {
        var (client, capture) = Build("/presence");

        await client.GetAsync("/presence/heartbeat");

        capture.LastUri!.Scheme.Should().Be("http");
        capture.LastUri!.Host.Should().Be("gw");
        capture.LastUri!.Port.Should().Be(8090);
    }

    [Fact]
    public async Task RewritesNonGetMethodsToo()
    {
        var (client, capture) = Build("/messaging");

        await client.PostAsync("/messages/", new StringContent(""));

        capture.LastUri!.AbsolutePath.Should().Be("/messaging/messages/");
    }
}
