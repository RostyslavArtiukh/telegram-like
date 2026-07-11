using FluentValidation;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Application.Commands.RenameChat;

public sealed class RenameChatCommandValidator : AbstractValidator<RenameChatCommand>
{
    public RenameChatCommandValidator()
    {
        RuleFor(x => x.ChatId).NotEmpty();
        RuleFor(x => x.RenamedByUserId).NotEmpty();
        RuleFor(x => x.NewName).NotEmpty().MaximumLength(ChatName.MaxLength);
    }
}
