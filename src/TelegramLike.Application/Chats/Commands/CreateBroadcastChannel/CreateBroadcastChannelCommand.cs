using MediatR;

namespace TelegramLike.Application.Chats.Commands.CreateBroadcastChannel;

public sealed record CreateBroadcastChannelCommand(Guid OwnerUserId, string Name) : IRequest<Guid>;
