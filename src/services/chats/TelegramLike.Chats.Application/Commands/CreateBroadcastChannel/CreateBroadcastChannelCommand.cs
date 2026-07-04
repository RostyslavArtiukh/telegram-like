using MediatR;

namespace TelegramLike.Chats.Application.Commands.CreateBroadcastChannel;

// ChatId is the client-supplied idempotency key; empty => the handler mints one.
public sealed record CreateBroadcastChannelCommand(Guid ChatId, Guid OwnerUserId, string Name) : IRequest<Guid>;
