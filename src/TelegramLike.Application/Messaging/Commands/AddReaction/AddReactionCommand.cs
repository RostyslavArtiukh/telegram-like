using MediatR;
using TelegramLike.Domain.Messaging.ValueObjects;

namespace TelegramLike.Application.Messaging.Commands.AddReaction;

public sealed record AddReactionCommand(Guid MessageId, Guid UserId, Emoji Emoji) : IRequest;
