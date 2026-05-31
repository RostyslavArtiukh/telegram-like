using MediatR;

namespace TelegramLike.Chats.Application.Commands.TransferOwnership;

public sealed record TransferOwnershipCommand(
    Guid ChatId,
    Guid NewOwnerUserId,
    Guid CurrentOwnerUserId) : IRequest;
