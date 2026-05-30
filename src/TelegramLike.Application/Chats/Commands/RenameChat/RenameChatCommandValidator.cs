using FluentValidation;
using TelegramLike.Domain.Chats.ValueObjects;

namespace TelegramLike.Application.Chats.Commands.RenameChat;

public sealed class RenameChatCommandValidator : AbstractValidator<RenameChatCommand>
{
    public RenameChatCommandValidator()
    {
        RuleFor(x => x.ChatId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.NewName).NotEmpty().MaximumLength(ChatName.MaxLength);
    }
}
