using System.Net.Http.Headers;

namespace TelegramLike.Web.Services.NotificationsApi;

/// Reads the caller-supplied `userId` from request.Options (set by per-service typed
/// HttpClients), mints a short-lived HMAC-signed JWT for that user, and attaches it
/// as Bearer. We avoid resolving Blazor's AuthenticationStateProvider here because
/// the handler lives in a different DI scope than the Razor circuit.
///
/// `UserIdKey` is the shared option key — all downstream service clients write to it.
internal sealed class ServiceAuthHandler(ServiceTokenIssuer tokenIssuer) : DelegatingHandler
{
    public static readonly HttpRequestOptionsKey<Guid> UserIdKey = new("ServiceAuth.UserId");

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Options.TryGetValue(UserIdKey, out var userId) && userId != Guid.Empty)
        {
            var token = tokenIssuer.IssueForUser(userId);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
