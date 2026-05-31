using FluentValidation;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Application.Commands.SendMessage;

public sealed class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(x => x.ChatId).NotEmpty();
        RuleFor(x => x.AuthorId).NotEmpty();

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Text) || (x.Attachments is { Count: > 0 }))
            .WithMessage("Message must contain text or at least one attachment.");

        When(x => !string.IsNullOrWhiteSpace(x.Text), () =>
        {
            RuleFor(x => x.Text!).MaximumLength(MessageContent.MaxTextLength);
        });

        RuleFor(x => x)
            .Must(x => (x.ForwardOriginalMessageId is null) == (x.ForwardOriginalChatId is null))
            .WithMessage("Forward reference requires both message id and chat id.");
    }
}
