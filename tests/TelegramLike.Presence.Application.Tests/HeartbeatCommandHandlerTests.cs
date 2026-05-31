using FluentAssertions;
using MassTransit;
using NSubstitute;
using TelegramLike.Contracts.Presence;
using TelegramLike.Presence.Application.Abstractions;
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
    public async Task Already_online_only_touches_cache_and_does_not_publish()
    {
        var userId = Guid.NewGuid();
        var existing = UserPresence.CreateOffline(userId);
        existing.GoOnline(DateTime.UtcNow);
        _repo.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(existing);

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

        await Handler.Handle(new HeartbeatCommand(userId), CancellationToken.None);

        await _repo.Received(1).UpsertAsync(
            Arg.Is<UserPresence>(p => p.Status == OnlineStatus.Online),
            Arg.Any<CancellationToken>());
        await _publish.Received(1).Publish(
            Arg.Is<UserCameOnlineIntegrationEvent>(e => e.UserId == userId),
            Arg.Any<CancellationToken>());
    }
}
