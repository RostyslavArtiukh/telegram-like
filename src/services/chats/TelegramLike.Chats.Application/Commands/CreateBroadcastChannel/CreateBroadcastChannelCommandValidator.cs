using FluentValidation;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Application.Commands.CreateBroadcastChannel;

public sealed class CreateBroadcastChannelCommandValidator : AbstractValidator<CreateBroadcastChannelCommand>
{
    public CreateBroadcastChannelCommandValidator()
    {
        RuleFor(x => x.OwnerUserId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ChatName.MaxLength);
    }
}
