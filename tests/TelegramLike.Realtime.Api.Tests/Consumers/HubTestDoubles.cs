using MassTransit;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using TelegramLike.Realtime.Api.Hubs;

namespace TelegramLike.Realtime.Api.Tests.Consumers;

/// <summary>
/// Shared NSubstitute wiring for consumer tests: a mocked IHubContext&lt;RealtimeHub&gt;
/// backed by a mocked IHubClients (tests stub Group/Groups/All to return per-target
/// IClientProxy mocks and assert SendCoreAsync calls on those), plus a hand-built
/// ConsumeContext&lt;T&gt; so consumers can be exercised without a running bus.
/// </summary>
internal static class HubTestDoubles
{
    public static (IHubContext<RealtimeHub> Hub, IHubClients Clients) Create()
    {
        var clients = Substitute.For<IHubClients>();
        var hub = Substitute.For<IHubContext<RealtimeHub>>();
        hub.Clients.Returns(clients);
        return (hub, clients);
    }

    public static ConsumeContext<T> ContextFor<T>(T message) where T : class
    {
        var context = Substitute.For<ConsumeContext<T>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    /// <summary>
    /// Matches a SendCoreAsync args array carrying exactly one payload of type T
    /// satisfying <paramref name="predicate"/>. Pulled out as a plain method (rather
    /// than inlining an `is T value` pattern) because Arg.Is's Expression&lt;Predicate&lt;T&gt;&gt;
    /// overload can't contain C# pattern-matching syntax (CS8122).
    /// </summary>
    public static bool SinglePayload<T>(object?[] args, Func<T, bool> predicate)
        => args.Length == 1 && args[0] is T value && predicate(value);
}
