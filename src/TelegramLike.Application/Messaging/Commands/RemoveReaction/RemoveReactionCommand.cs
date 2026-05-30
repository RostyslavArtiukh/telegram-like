using MediatR;
using TelegramLike.Domain.Messaging.ValueObjects;

namespace TelegramLike.Application.Messaging.Commands.RemoveReaction;

public sealed record RemoveReactionCommand(Guid MessageId, Guid UserId, Emoji Emoji) : IRequest;
