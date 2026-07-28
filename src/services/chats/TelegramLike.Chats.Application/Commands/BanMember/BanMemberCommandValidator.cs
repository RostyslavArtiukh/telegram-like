using FluentValidation;

namespace TelegramLike.Chats.Application.Commands.BanMember;

public sealed class BanMemberCommandValidator : AbstractValidator<BanMemberCommand>
{
    /// <summary>Free-text moderation note; capped so it can't be used to store arbitrary blobs.</summary>
    public const int MaxReasonLength = 512;

    public BanMemberCommandValidator()
    {
        RuleFor(x => x.ChatId).NotEmpty();
        RuleFor(x => x.MemberToBanUserId).NotEmpty();
        RuleFor(x => x.BannedByUserId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(MaxReasonLength).When(x => x.Reason is not null);
    }
}
