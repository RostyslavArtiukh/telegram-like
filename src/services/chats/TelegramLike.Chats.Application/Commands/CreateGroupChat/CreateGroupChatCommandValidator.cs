using FluentValidation;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Application.Commands.CreateGroupChat;

public sealed class CreateGroupChatCommandValidator : AbstractValidator<CreateGroupChatCommand>
{
    public CreateGroupChatCommandValidator()
    {
        RuleFor(x => x.OwnerUserId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ChatName.MaxLength);
    }
}
