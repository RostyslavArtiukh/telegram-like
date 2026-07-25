using MediatR;
using TelegramLike.Identity.Application.Security;

namespace TelegramLike.Identity.Application.Commands.EndSession;

public sealed class EndSessionCommandHandler(ISessionService sessionService)
    : IRequestHandler<EndSessionCommand>
{
    public async Task Handle(EndSessionCommand request, CancellationToken cancellationToken)
    {
        // Nothing to revoke for an empty token; the store delete is a no-op anyway.
        if (string.IsNullOrWhiteSpace(request.SessionToken)) return;

        await sessionService.DeleteSessionAsync(request.SessionToken, cancellationToken);
    }
}
