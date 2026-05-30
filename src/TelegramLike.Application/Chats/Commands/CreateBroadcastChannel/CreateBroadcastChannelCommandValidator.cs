using FluentValidation;
using TelegramLike.Domain.Chats.ValueObjects;

namespace TelegramLike.Application.Chats.Commands.CreateBroadcastChannel;

public sealed class CreateBroadcastChannelCommandValidator : AbstractValidator<CreateBroadcastChannelCommand>
{
    public CreateBroadcastChannelCommandValidator()
    {
        RuleFor(x => x.OwnerUserId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ChatName.MaxLength);
    }
}
