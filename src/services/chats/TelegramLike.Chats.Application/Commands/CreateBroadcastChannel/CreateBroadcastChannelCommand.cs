using MediatR;

namespace TelegramLike.Chats.Application.Commands.CreateBroadcastChannel;

public sealed record CreateBroadcastChannelCommand(Guid OwnerUserId, string Name) : IRequest<Guid>;
