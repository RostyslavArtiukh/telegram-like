using FluentAssertions;
using TelegramLike.Messaging.Infrastructure.Storage;
using TelegramLike.Messaging.Tests.Infrastructure.Fixtures;

namespace TelegramLike.Messaging.Tests.Infrastructure;

[Collection(MongoCollection.Name)]
public class MessageReadReceiptRepositoryIntegrationTests(MongoFixture fx)
{
    private MessageReadReceiptRepository NewRepository() => new(fx.Database);

    [Fact]
    public async Task MarkAsRead_FirstForReader_ReturnsTrueAndIsRecorded()
    {
        var repo = NewRepository();
        var messageId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var created = await repo.MarkAsReadAsync(messageId, memberId, DateTime.UtcNow);

        created.Should().BeTrue();
        (await repo.HasReceiptAsync(messageId, memberId)).Should().BeTrue();
    }

    [Fact]
    public async Task MarkAsRead_RepeatForSameReader_ReturnsFalseIdempotently()
    {
        var repo = NewRepository();
        var messageId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var first = await repo.MarkAsReadAsync(messageId, memberId, DateTime.UtcNow);
        var second = await repo.MarkAsReadAsync(messageId, memberId, DateTime.UtcNow);

        first.Should().BeTrue();
        second.Should().BeFalse("the unique (MessageId, MemberId) index backs the idempotent no-op");
    }

    [Fact]
    public async Task MarkAsRead_ConcurrentForSameReader_OnlyOneWins()
    {
        var repo = NewRepository();
        var messageId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var results = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => repo.MarkAsReadAsync(messageId, memberId, DateTime.UtcNow)));

        results.Count(r => r).Should().Be(1, "only the first insert should win the unique-index race");
    }

    [Fact]
    public async Task MarkAsRead_DifferentReadersOfSameMessage_EachGetOwnReceipt()
    {
        var repo = NewRepository();
        var messageId = Guid.NewGuid();

        var a = await repo.MarkAsReadAsync(messageId, Guid.NewGuid(), DateTime.UtcNow);
        var b = await repo.MarkAsReadAsync(messageId, Guid.NewGuid(), DateTime.UtcNow);

        a.Should().BeTrue();
        b.Should().BeTrue();
    }

    [Fact]
    public async Task HasReceipt_UnknownPair_ReturnsFalse()
    {
        var repo = NewRepository();

        (await repo.HasReceiptAsync(Guid.NewGuid(), Guid.NewGuid())).Should().BeFalse();
    }
}
