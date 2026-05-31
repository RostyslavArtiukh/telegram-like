using MediatR;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Application.Commands.RemoveReaction;

public sealed record RemoveReactionCommand(Guid MessageId, Guid UserId, Emoji Emoji) : IRequest;
