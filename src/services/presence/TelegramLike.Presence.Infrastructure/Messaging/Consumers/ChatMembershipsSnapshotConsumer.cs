using MassTransit;
using TelegramLike.Contracts.Chats;
using TelegramLike.Presence.Application.Storage;

namespace TelegramLike.Presence.Infrastructure.Messaging.Consumers;

// Backfill: materializes a chat's historical active membership into the local read-model.
// Each entry is applied last-writer-wins by its own JoinedAt, so a live MemberLeft that already
// ran always wins over the snapshot — re-running the backfill is safe. Presence tracks no role.
internal sealed class ChatMembershipsSnapshotConsumer(IChatMembershipReadModel readModel)
    : IConsumer<ChatMembershipsSnapshotIntegrationEvent>
{
    public async Task Consume(ConsumeContext<ChatMembershipsSnapshotIntegrationEvent> context)
    {
        foreach (var member in context.Message.Members)
        {
            await readModel.UpsertActiveAsync(
                context.Message.ChatId, member.UserId, member.JoinedAt, context.CancellationToken);
        }
    }
}
