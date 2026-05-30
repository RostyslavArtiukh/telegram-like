using MediatR;

namespace TelegramLike.Application.Chats.Commands.TransferOwnership;

public sealed record TransferOwnershipCommand(
    Guid ChatId,
    Guid NewOwnerUserId,
    Guid CurrentOwnerUserId) : IRequest;
