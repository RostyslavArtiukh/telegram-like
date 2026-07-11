using MassTransit;
using TelegramLike.Contracts.Chats;
using TelegramLike.Messaging.Application.Storage;

namespace TelegramLike.Messaging.Infrastructure.Messaging.Consumers;

// Keeps the materialized role current so retract can verify moderator authority
// server-side. Ordering is guarded by OccurredAt (last-writer-wins) in the read-model.
internal sealed class MemberRoleChangedConsumer(IChatMembershipReadModel readModel)
    : IConsumer<MemberRoleChangedIntegrationEvent>
{
    public Task Consume(ConsumeContext<MemberRoleChangedIntegrationEvent> context) =>
        readModel.SetRoleAsync(
            context.Message.ChatId, context.Message.UserId, context.Message.Role,
            context.Message.OccurredAt, context.CancellationToken);
}
