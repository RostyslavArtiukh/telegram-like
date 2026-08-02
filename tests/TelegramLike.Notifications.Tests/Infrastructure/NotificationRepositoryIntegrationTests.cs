using FluentAssertions;
using TelegramLike.Notifications.Domain.Aggregates;
using TelegramLike.Notifications.Domain.ValueObjects;
using TelegramLike.Notifications.Infrastructure.Storage;
using TelegramLike.Notifications.Tests.Infrastructure.Fixtures;

namespace TelegramLike.Notifications.Tests.Infrastructure;

[Collection(MongoCollection.Name)]
public class NotificationRepositoryIntegrationTests(MongoFixture fx)
{
    private NotificationRepository NewRepo() => new(fx.Database);
    private NotificationQueryService NewQuery() => new(fx.Database);

    private static Notification New(Guid recipient) => Notification.Create(
        recipient,
        NotificationType.NewMessage,
        NotificationPayload.ForNewMessage(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
        Guid.NewGuid());

    [Fact]
    public async Task AddMany_ThenGetUnreadCount_MatchesInput()
    {
        var repo = NewRepo();
        var query = NewQuery();
        var recipient = Guid.NewGuid();
        var batch = new[] { New(recipient), New(recipient), New(recipient) };

        await repo.AddManyIgnoringDuplicatesAsync(batch);
        var count = await query.GetUnreadCountAsync(recipient);

        count.Should().Be(3);
    }

    [Fact]
    public async Task MarkAllAsRead_ZerosOutUnreadCount()
    {
        var repo = NewRepo();
        var query = NewQuery();
        var recipient = Guid.NewGuid();
        await repo.AddManyIgnoringDuplicatesAsync(new[] { New(recipient), New(recipient) });

        await repo.MarkAllAsReadAsync(recipient, DateTime.UtcNow);
        var count = await query.GetUnreadCountAsync(recipient);

        count.Should().Be(0);
    }

    [Fact]
    public async Task Feed_UnreadOnly_ExcludesReadItems()
    {
        var repo = NewRepo();
        var query = NewQuery();
        var recipient = Guid.NewGuid();
        var read = New(recipient);
        read.MarkAsRead();
        await repo.AddAsync(read);
        await repo.AddAsync(New(recipient));

        var feed = await query.GetFeedAsync(recipient, beforeCreatedAt: null, pageSize: 10, unreadOnly: true);

        feed.Items.Should().ContainSingle()
            .Which.Status.Should().NotBe(NotificationStatus.Read);
    }

    [Fact]
    public async Task MarkAsRead_ViaRepository_RoundTripsStatus()
    {
        var repo = NewRepo();
        var recipient = Guid.NewGuid();
        var n = New(recipient);
        await repo.AddAsync(n);

        n.MarkAsRead();
        await repo.UpdateAsync(n);

        var loaded = await repo.GetByIdAsync(n.Id);
        loaded!.Status.Should().Be(NotificationStatus.Read);
        loaded.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AddManyIgnoringDuplicates_DedupesRedeliveredEventPerRecipient()
    {
        await NotificationIndexes.EnsureIndexesAsync(fx.Database);

        var repo = NewRepo();
        var query = NewQuery();
        var recipient = Guid.NewGuid();
        var sourceEventId = Guid.NewGuid();
        var payload = NotificationPayload.ForNewMessage(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // First delivery — both recipients land
        var firstBatch = new[]
        {
            Notification.Create(recipient, NotificationType.NewMessage, payload, sourceEventId),
            Notification.Create(Guid.NewGuid(), NotificationType.NewMessage, payload, sourceEventId)
        };
        var firstInserted = await repo.AddManyIgnoringDuplicatesAsync(firstBatch);

        // Simulate RabbitMQ redelivery — same SourceEventId, same recipient
        var secondBatch = new[]
        {
            Notification.Create(recipient, NotificationType.NewMessage, payload, sourceEventId)
        };
        var secondInserted = await repo.AddManyIgnoringDuplicatesAsync(secondBatch);

        firstInserted.Should().Be(2);
        secondInserted.Should().Be(0);

        var count = await query.GetUnreadCountAsync(recipient);
        count.Should().Be(1, "the redelivered event must NOT create a second notification");
    }
}
