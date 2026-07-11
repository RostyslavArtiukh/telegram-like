using MediatR;
using TelegramLike.Identity.Application.Security;
using TelegramLike.Identity.Domain.Aggregates;
using TelegramLike.Identity.Domain.Repositories;

namespace TelegramLike.Identity.Application.Auth.ExchangeSession;

public sealed class ExchangeSessionQueryHandler(
    ISessionService sessionService,
    IUserRepository userRepository,
    IAccessTokenIssuer accessTokenIssuer)
    : IRequestHandler<ExchangeSessionQuery, SessionExchangeDto?>
{
    public async Task<SessionExchangeDto?> Handle(
        ExchangeSessionQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SessionToken)) return null;

        var userId = await sessionService.GetUserIdAsync(request.SessionToken, cancellationToken);
        if (userId is null) return null;

        var user = await userRepository.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null) return null;

        // A session that outlived a ban/soft-delete must stop minting access JWTs.
        // Returning null makes the Web BFF treat the session as invalid.
        if (user.Status != AccountStatus.Active) return null;

        var token = accessTokenIssuer.IssueForUser(user.Id);
        return new SessionExchangeDto(
            user.Id,
            user.Username.Value,
            user.Email.Value,
            token.Token,
            token.ExpiresInSeconds);
    }
}
