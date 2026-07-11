using FluentAssertions;
using MassTransit;
using NSubstitute;
using TelegramLike.Contracts.Chats;
using TelegramLike.Messaging.Infrastructure.Messaging.Consumers;
using TelegramLike.Messaging.Infrastructure.Storage;
using TelegramLike.Messaging.Tests.Infrastructure.Fixtures;

namespace TelegramLike.Messaging.Tests.Infrastructure;

// The consumers are thin pass-throughs onto IChatMembershipReadModel; exercising them
// against the real Mongo-backed read model (rather than a mocked interface) verifies
// both the wiring (right method, right arguments) and that RabbitMQ's at-least-once
// redelivery is safe end-to-end: a duplicate delivery must leave the read model in the
// same state as a single delivery.
[Collection(MongoCollection.Name)]
public class MembershipConsumersIntegrationTests(MongoFixture fx)
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
    public async Task MemberJoinedConsumer_duplicate_delivery_leaves_member_active_once()
    {
        var readModel = NewReadModel();
        var consumer = new MemberJoinedConsumer(readModel);
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var evt = new MemberJoinedIntegrationEvent(Guid.NewGuid(), T0, chatId, userId, [userId], "Member");

        await consumer.Consume(ContextFor(evt));
        await consumer.Consume(ContextFor(evt)); // redelivery

        (await readModel.IsActiveMemberAsync(chatId, userId)).Should().BeTrue();
        (await readModel.GetActiveMemberIdsAsync(chatId)).Should().ContainSingle().Which.Should().Be(userId);
    }

    [Fact]
    public async Task MemberLeftConsumer_duplicate_delivery_leaves_member_inactive_once()
    {
        var readModel = NewReadModel();
        await readModel.UpsertActiveAsync(Guid.NewGuid(), Guid.NewGuid(), "Member", T0); // noise
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await readModel.UpsertActiveAsync(chatId, userId, "Member", T0);

        var consumer = new MemberLeftConsumer(readModel);
        var evt = new MemberLeftIntegrationEvent(Guid.NewGuid(), T0.AddSeconds(1), chatId, userId);

        await consumer.Consume(ContextFor(evt));
        await consumer.Consume(ContextFor(evt)); // redelivery

        (await readModel.IsActiveMemberAsync(chatId, userId)).Should().BeFalse();
    }

    [Fact]
    public async Task MemberKickedConsumer_duplicate_delivery_leaves_member_inactive_once()
    {
        var readModel = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await readModel.UpsertActiveAsync(chatId, userId, "Member", T0);

        var consumer = new MemberKickedConsumer(readModel);
        var evt = new MemberKickedIntegrationEvent(Guid.NewGuid(), T0.AddSeconds(1), chatId, userId, Guid.NewGuid(), [userId]);

        await consumer.Consume(ContextFor(evt));
        await consumer.Consume(ContextFor(evt)); // redelivery

        (await readModel.IsActiveMemberAsync(chatId, userId)).Should().BeFalse();
    }

    [Fact]
    public async Task MemberRoleChangedConsumer_duplicate_delivery_leaves_role_applied_once()
    {
        var readModel = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await readModel.UpsertActiveAsync(chatId, userId, "Member", T0);

        var consumer = new MemberRoleChangedConsumer(readModel);
        var evt = new MemberRoleChangedIntegrationEvent(Guid.NewGuid(), T0.AddSeconds(1), chatId, userId, "Admin");

        await consumer.Consume(ContextFor(evt));
        await consumer.Consume(ContextFor(evt)); // redelivery

        (await readModel.IsModeratorAsync(chatId, userId)).Should().BeTrue();
    }

    [Fact]
    public async Task MemberRoleChangedConsumer_stale_redelivery_does_not_revert_a_newer_role()
    {
        var readModel = NewReadModel();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await readModel.UpsertActiveAsync(chatId, userId, "Member", T0);

        var consumer = new MemberRoleChangedConsumer(readModel);
        var promote = new MemberRoleChangedIntegrationEvent(Guid.NewGuid(), T0.AddSeconds(10), chatId, userId, "Admin");
        var staleDemote = new MemberRoleChangedIntegrationEvent(Guid.NewGuid(), T0.AddSeconds(1), chatId, userId, "Member");

        await consumer.Consume(ContextFor(promote));
        await consumer.Consume(ContextFor(staleDemote)); // out-of-order redelivery

        (await readModel.IsModeratorAsync(chatId, userId)).Should().BeTrue(
            "a stale role event delivered after a newer one must not revert the role");
    }
}
