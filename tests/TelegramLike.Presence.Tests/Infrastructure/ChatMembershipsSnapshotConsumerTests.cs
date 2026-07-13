using FluentAssertions;
using MassTransit;
using NSubstitute;
using TelegramLike.Contracts.Chats;
using TelegramLike.Presence.Infrastructure.Messaging.Consumers;
using TelegramLike.Presence.Infrastructure.Storage;
using TelegramLike.Presence.Tests.Infrastructure.Fixtures;

namespace TelegramLike.Presence.Tests.Infrastructure;

// [TL-101] backfill: the snapshot consumer materializes a chat's historical membership into the
// Mongo-backed read model. Presence tracks no role; only active/inactive state (LWW by JoinedAt).
[Collection(MongoCollection.Name)]
public class ChatMembershipsSnapshotConsumerTests(MongoFixture fx)
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private MongoChatMembershipReadModel NewReadModel() => new(fx.Database);

    private static ConsumeContext<T> ContextFor<T>(T message) where T : class
    {
        var ctx = Substitute.For<ConsumeContext<T>>();
        ctx.Message.Returns(message);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    [Fact]
    public async Task MaterializesAllActiveMembers_Idempotently()
    {
        var readModel = NewReadModel();
        var consumer = new ChatMembershipsSnapshotConsumer(readModel);
        var chatId = Guid.NewGuid();
        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();
        var evt = new ChatMembershipsSnapshotIntegrationEvent(Guid.NewGuid(), T0.AddSeconds(60), chatId,
        [
            new ChatMembershipSnapshotEntry(u1, "Owner", T0),
            new ChatMembershipSnapshotEntry(u2, "Member", T0),
        ]);

        await consumer.Consume(ContextFor(evt));
        await consumer.Consume(ContextFor(evt)); // redelivery must be a no-op

        (await readModel.IsActiveMemberAsync(chatId, u1)).Should().BeTrue();
        (await readModel.IsActiveMemberAsync(chatId, u2)).Should().BeTrue();
    }

    [Fact]
    public async Task StaleSnapshot_DoesNotResurrectLeftMember()
    {
        var readModel = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await readModel.UpsertActiveAsync(chatId, userId, T0);
        await readModel.DeactivateAsync(chatId, userId, T0.AddSeconds(10)); // left after joining

        var consumer = new ChatMembershipsSnapshotConsumer(readModel);
        var evt = new ChatMembershipsSnapshotIntegrationEvent(Guid.NewGuid(), T0.AddSeconds(60), chatId,
            [new ChatMembershipSnapshotEntry(userId, "Member", T0)]); // JoinedAt = T0, older than the leave

        await consumer.Consume(ContextFor(evt));

        (await readModel.IsActiveMemberAsync(chatId, userId)).Should().BeFalse(
            "the snapshot's JoinedAt is older than the processed leave, so LWW must not resurrect the member");
    }
}
