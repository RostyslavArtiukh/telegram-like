using FluentAssertions;
using MassTransit;
using NSubstitute;
using TelegramLike.Contracts.Presence;
using TelegramLike.Presence.Application.Storage;
using TelegramLike.Presence.Application.Commands.Heartbeat;
using TelegramLike.Presence.Domain.Aggregates;
using TelegramLike.Presence.Domain.Repositories;
using TelegramLike.Presence.Domain.ValueObjects;

namespace TelegramLike.Presence.Application.Tests;

public class HeartbeatCommandHandlerTests
{
    private readonly IUserPresenceRepository _repo = Substitute.For<IUserPresenceRepository>();
    private readonly IPresenceCache _cache = Substitute.For<IPresenceCache>();
    private readonly IPublishEndpoint _publish = Substitute.For<IPublishEndpoint>();

    private HeartbeatCommandHandler Handler => new(_repo, _cache, _publish);

    [Fact]
    public async Task Empty_user_id_throws()
    {
        var act = () => Handler.Handle(new HeartbeatCommand(Guid.Empty), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task First_heartbeat_creates_presence_and_transitions_to_online()
    {
        var userId = Guid.NewGuid();
        _repo.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns((UserPresence?)null);

        UserPresence? captured = null;
        _repo.UpsertAsync(Arg.Do<UserPresence>(p => captured = p), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await Handler.Handle(new HeartbeatCommand(userId), CancellationToken.None);

        await _cache.Received(1).TouchAsync(userId, Arg.Any<CancellationToken>());
        captured!.Status.Should().Be(OnlineStatus.Online);
    }

    [Fact]
    public async Task Still_online_per_redis_only_touches_cache_and_does_not_publish()
    {
        // "Online" is decided by the Redis heartbeat key, not the durable Mongo Status.
        var userId = Guid.NewGuid();
        _cache.IsOnlineAsync(userId, Arg.Any<CancellationToken>()).Returns(true);

        await Handler.Handle(new HeartbeatCommand(userId), CancellationToken.None);

        await _cache.Received(1).TouchAsync(userId, Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().UpsertAsync(Arg.Any<UserPresence>(), Arg.Any<CancellationToken>());
        await _publish.DidNotReceive().Publish(
            Arg.Any<UserCameOnlineIntegrationEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Was_offline_transitions_back_to_online_and_publishes_event()
    {
        var userId = Guid.NewGuid();
        var existing = UserPresence.CreateOffline(userId);
        _repo.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(existing);
        _cache.IsOnlineAsync(userId, Arg.Any<CancellationToken>()).Returns(false);

        await Handler.Handle(new HeartbeatCommand(userId), CancellationToken.None);

        await _repo.Received(1).UpsertAsync(
            Arg.Is<UserPresence>(p => p.Status == OnlineStatus.Online),
            Arg.Any<CancellationToken>());
        await _publish.Received(1).Publish(
            Arg.Is<UserCameOnlineIntegrationEvent>(e => e.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reconnect_after_ttl_expiry_republishes_even_when_mongo_status_is_stale_online()
    {
        // B5: the heartbeat key lapsed (browser close) but Mongo Status was never
        // reconciled and still reads Online. Redis is offline, so this heartbeat is a
        // genuine reconnect and MUST re-publish UserCameOnline — gating on Mongo would
        // permanently swallow the event.
        var userId = Guid.NewGuid();
        var stale = UserPresence.CreateOffline(userId);
        stale.GoOnline(DateTime.UtcNow); // Mongo says Online
        _repo.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(stale);
        _cache.IsOnlineAsync(userId, Arg.Any<CancellationToken>()).Returns(false); // Redis expired

        await Handler.Handle(new HeartbeatCommand(userId), CancellationToken.None);

        await _publish.Received(1).Publish(
            Arg.Is<UserCameOnlineIntegrationEvent>(e => e.UserId == userId),
            Arg.Any<CancellationToken>());
    }
}
