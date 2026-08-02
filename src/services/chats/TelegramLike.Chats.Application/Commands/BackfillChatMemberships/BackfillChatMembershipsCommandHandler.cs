using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using TelegramLike.Chats.Application.Backfill;
using TelegramLike.Contracts.Chats;
using TelegramLike.Shared.Application;

namespace TelegramLike.Chats.Application.Commands.BackfillChatMemberships;

public sealed class BackfillChatMembershipsCommandHandler(
    IChatMembershipBackfillReader reader,
    IPublishEndpoint publishEndpoint,
    ILogger<BackfillChatMembershipsCommandHandler> logger)
    : IRequestHandler<BackfillChatMembershipsCommand, BackfillChatMembershipsResult>
{
    public async Task<BackfillChatMembershipsResult> Handle(
        BackfillChatMembershipsCommand request, CancellationToken cancellationToken)
    {
        var snapshots = await reader.GetActiveMembershipsByChatAsync(cancellationToken);

        var members = 0;
        foreach (var snapshot in snapshots)
        {
            var entries = snapshot.Members
                .Select(m => new ChatMembershipSnapshotEntry(m.UserId, m.Role, m.JoinedAt))
                .ToList();

            // Direct publish (not via the outbox): this is an out-of-band admin operation, the
            // events are idempotent, and at-least-once delivery is exactly what the consumers expect.
            //
            // Split into parts ([TL-124]) — a snapshot of a large chat was the biggest single
            // message this system ever produced, and consumers apply entries one at a time, so
            // parts need no coordination. Publish by concrete type: MassTransit routes on the
            // declared type, and the interface would land on the wrong exchange.
            var parts = FanoutParts.Split(
                entries,
                Guid.NewGuid(),
                (id, part, index, count) => new ChatMembershipsSnapshotIntegrationEvent(
                    id, DateTime.UtcNow, snapshot.ChatId, part, index, count));

            foreach (var part in parts)
                await publishEndpoint.Publish(part, part.GetType(), cancellationToken);

            // Chat-type backfill ([TL-102]): materialize each pre-existing chat's type so
            // SendMessage can derive isBroadcast server-side. Set-once, idempotent.
            await publishEndpoint.Publish(
                new ChatCreatedIntegrationEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAt: DateTime.UtcNow,
                    ChatId: snapshot.ChatId,
                    Type: snapshot.ChatType),
                cancellationToken);

            members += snapshot.Members.Count;
        }

        logger.LogInformation(
            "Chat membership backfill published {ChatCount} chat snapshots covering {MemberCount} active memberships.",
            snapshots.Count, members);

        return new BackfillChatMembershipsResult(snapshots.Count, members);
    }
}
