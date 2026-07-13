using FluentAssertions;
using MassTransit;
using NSubstitute;
using TelegramLike.Contracts.Chats;
using TelegramLike.Messaging.Infrastructure.Messaging.Consumers;
using TelegramLike.Messaging.Infrastructure.Storage;
using TelegramLike.Messaging.Tests.Infrastructure.Fixtures;

namespace TelegramLike.Messaging.Tests.Infrastructure;

// [TL-102] chat-type read-model + its ChatCreated consumer, over real Mongo.
[Collection(MongoCollection.Name)]
public class ChatTypeReadModelTests(MongoFixture fx)
{
    private MongoChatTypeReadModel NewReadModel() => new(fx.Database);

    private static ConsumeContext<T> ContextFor<T>(T message) where T : class
    {
        var ctx = Substitute.For<ConsumeContext<T>>();
        ctx.Message.Returns(message);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    [Fact]
    public async Task IsBroadcast_UnknownChat_ReturnsNull()
    {
        (await NewReadModel().IsBroadcastAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Theory]
    [InlineData("Broadcast", true)]
    [InlineData("Group", false)]
    [InlineData("Direct", false)]
    public async Task Consumer_MaterializesType_AndIsBroadcastReflectsIt(string type, bool expected)
    {
        var readModel = NewReadModel();
        var consumer = new ChatCreatedConsumer(readModel);
        var chatId = Guid.NewGuid();

        var evt = new ChatCreatedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, chatId, type);
        await consumer.Consume(ContextFor(evt));
        await consumer.Consume(ContextFor(evt)); // redelivery is a no-op (set-once)

        (await readModel.IsBroadcastAsync(chatId)).Should().Be(expected);
    }

    [Fact]
    public async Task Upsert_IsSetOnce_FirstWriteWins()
    {
        var readModel = NewReadModel();
        var chatId = Guid.NewGuid();

        await readModel.UpsertAsync(chatId, "Broadcast");
        await readModel.UpsertAsync(chatId, "Group"); // must not overwrite

        (await readModel.IsBroadcastAsync(chatId)).Should().BeTrue();
    }
}
