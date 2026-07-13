using MassTransit;
using TelegramLike.Contracts.Chats;
using TelegramLike.Messaging.Application.Storage;

namespace TelegramLike.Messaging.Infrastructure.Messaging.Consumers;

// Backfill: materializes a chat's historical active membership into the local read-model.
// Each entry is applied last-writer-wins by its own JoinedAt, so a live MemberLeft/RoleChanged
// that already ran always wins over the snapshot — re-running the backfill is safe.
internal sealed class ChatMembershipsSnapshotConsumer(IChatMembershipReadModel readModel)
    : IConsumer<ChatMembershipsSnapshotIntegrationEvent>
{
    public async Task Consume(ConsumeContext<ChatMembershipsSnapshotIntegrationEvent> context)
    {
        foreach (var member in context.Message.Members)
        {
            await readModel.UpsertActiveAsync(
                context.Message.ChatId, member.UserId, member.Role, member.JoinedAt, context.CancellationToken);
        }
    }
}
