using FluentValidation;
using TelegramLike.Domain.Chats.ValueObjects;

namespace TelegramLike.Application.Chats.Commands.CreateGroupChat;

public sealed class CreateGroupChatCommandValidator : AbstractValidator<CreateGroupChatCommand>
{
    public CreateGroupChatCommandValidator()
    {
        RuleFor(x => x.OwnerUserId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ChatName.MaxLength);
    }
}
