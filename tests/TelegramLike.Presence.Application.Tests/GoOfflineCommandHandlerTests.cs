using FluentAssertions;
using MassTransit;
using NSubstitute;
using TelegramLike.Contracts.Presence;
using TelegramLike.Presence.Application.Abstractions;
using TelegramLike.Presence.Application.Commands.GoOffline;
using TelegramLike.Presence.Domain.Aggregates;
using TelegramLike.Presence.Domain.Repositories;
using TelegramLike.Presence.Domain.ValueObjects;

namespace TelegramLike.Presence.Application.Tests;

public class GoOfflineCommandHandlerTests
{
    private readonly IUserPresenceRepository _repo = Substitute.For<IUserPresenceRepository>();
    private readonly IPresenceCache _cache = Substitute.For<IPresenceCache>();
    private readonly IPublishEndpoint _publish = Substitute.For<IPublishEndpoint>();

    private GoOfflineCommandHandler Handler => new(_repo, _cache, _publish);

    [Fact]
    public async Task Online_user_transitions_to_offline_and_publishes_event()
    {
        var userId = Guid.NewGuid();
        var existing = UserPresence.CreateOffline(userId);
        existing.GoOnline(DateTime.UtcNow);
        _repo.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(existing);

        await Handler.Handle(new GoOfflineCommand(userId), CancellationToken.None);

        await _cache.Received(1).ClearAsync(userId, Arg.Any<CancellationToken>());
        await _repo.Received(1).UpsertAsync(
            Arg.Is<UserPresence>(p => p.Status == OnlineStatus.Offline),
            Arg.Any<CancellationToken>());
        await _publish.Received(1).Publish(
            Arg.Is<UserWentOfflineIntegrationEvent>(e => e.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Already_offline_does_not_publish()
    {
        var userId = Guid.NewGuid();
        var existing = UserPresence.CreateOffline(userId);
        _repo.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(existing);

        await Handler.Handle(new GoOfflineCommand(userId), CancellationToken.None);

        // We still clear the Redis cache (heartbeat token might exist), but
        // emit no event — nothing transitioned.
        await _cache.Received(1).ClearAsync(userId, Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().UpsertAsync(Arg.Any<UserPresence>(), Arg.Any<CancellationToken>());
        await _publish.DidNotReceive().Publish(
            Arg.Any<UserWentOfflineIntegrationEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Empty_user_id_throws()
    {
        var act = () => Handler.Handle(new GoOfflineCommand(Guid.Empty), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
