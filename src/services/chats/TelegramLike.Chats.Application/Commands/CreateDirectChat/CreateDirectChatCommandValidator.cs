using FluentValidation;

namespace TelegramLike.Chats.Application.Commands.CreateDirectChat;

public sealed class CreateDirectChatCommandValidator : AbstractValidator<CreateDirectChatCommand>
{
    public CreateDirectChatCommandValidator()
    {
        RuleFor(x => x.InitiatorUserId).NotEmpty();
        RuleFor(x => x.PeerUserId).NotEmpty()
            .NotEqual(x => x.InitiatorUserId).WithMessage("Cannot create a direct chat with yourself.");
    }
}
