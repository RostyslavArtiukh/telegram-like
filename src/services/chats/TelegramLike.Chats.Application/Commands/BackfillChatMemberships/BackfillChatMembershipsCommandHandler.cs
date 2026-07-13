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
            // event is idempotent, and at-least-once delivery is exactly what the LWW consumers expect.
            await publishEndpoint.Publish(
                new ChatMembershipsSnapshotIntegrationEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAt: DateTime.UtcNow,
                    ChatId: snapshot.ChatId,
                    Members: snapshot.Members
                        .Select(m => new ChatMembershipSnapshotEntry(m.UserId, m.Role, m.JoinedAt))
                        .ToList()),
                cancellationToken);

            members += snapshot.Members.Count;
        }

        logger.LogInformation(
            "Chat membership backfill published {ChatCount} chat snapshots covering {MemberCount} active memberships.",
            snapshots.Count, members);

        return new BackfillChatMembershipsResult(snapshots.Count, members);
    }
}
