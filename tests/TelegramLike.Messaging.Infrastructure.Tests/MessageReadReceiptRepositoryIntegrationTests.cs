using FluentAssertions;
using TelegramLike.Messaging.Infrastructure.Persistence;
using TelegramLike.Messaging.Infrastructure.Tests.Fixtures;

namespace TelegramLike.Messaging.Infrastructure.Tests;

[Collection(MongoCollection.Name)]
public class MessageReadReceiptRepositoryIntegrationTests(MongoFixture fx)
{
    private MessageReadReceiptRepository NewRepository() => new(fx.Database);

    [Fact]
    public async Task First_MarkAsRead_for_a_reader_returns_true_and_is_recorded()
    {
        var repo = NewRepository();
        var messageId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var created = await repo.MarkAsReadAsync(messageId, memberId, DateTime.UtcNow);

        created.Should().BeTrue();
        (await repo.HasReceiptAsync(messageId, memberId)).Should().BeTrue();
    }

    [Fact]
    public async Task Repeat_MarkAsRead_for_the_same_reader_returns_false_idempotently()
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
    public async Task Concurrent_MarkAsRead_for_the_same_reader_only_one_wins()
    {
        var repo = NewRepository();
        var messageId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var results = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => repo.MarkAsReadAsync(messageId, memberId, DateTime.UtcNow)));

        results.Count(r => r).Should().Be(1, "only the first insert should win the unique-index race");
    }

    [Fact]
    public async Task Different_readers_of_the_same_message_each_get_their_own_receipt()
    {
        var repo = NewRepository();
        var messageId = Guid.NewGuid();

        var a = await repo.MarkAsReadAsync(messageId, Guid.NewGuid(), DateTime.UtcNow);
        var b = await repo.MarkAsReadAsync(messageId, Guid.NewGuid(), DateTime.UtcNow);

        a.Should().BeTrue();
        b.Should().BeTrue();
    }

    [Fact]
    public async Task HasReceipt_for_unknown_pair_returns_false()
    {
        var repo = NewRepository();

        (await repo.HasReceiptAsync(Guid.NewGuid(), Guid.NewGuid())).Should().BeFalse();
    }
}
