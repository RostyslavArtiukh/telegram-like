using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using TelegramLike.Chats.Application.Backfill;
using TelegramLike.Contracts.Chats;

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
            // Direct publish (not via the outbox): this is an out-of-band admin operation, the
            // events are idempotent, and at-least-once delivery is exactly what the consumers expect.
            await publishEndpoint.Publish(
                new ChatMembershipsSnapshotIntegrationEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAt: DateTime.UtcNow,
                    ChatId: snapshot.ChatId,
                    Members: snapshot.Members
                        .Select(m => new ChatMembershipSnapshotEntry(m.UserId, m.Role, m.JoinedAt))
                        .ToList()),
                cancellationToken);

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
