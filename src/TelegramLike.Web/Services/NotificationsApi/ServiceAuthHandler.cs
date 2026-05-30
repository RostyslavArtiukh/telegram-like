using System.Net.Http.Headers;

namespace TelegramLike.Web.Services.NotificationsApi;

/// Reads the caller-supplied `userId` from request.Options (set by NotificationsApiClient),
/// mints a short-lived HMAC-signed JWT for that user, and attaches it as Bearer.
/// We avoid resolving Blazor's AuthenticationStateProvider here because the handler
/// lives in a different DI scope than the Razor circuit.
internal sealed class ServiceAuthHandler(ServiceTokenIssuer tokenIssuer) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Options.TryGetValue(NotificationsApiClient.UserIdKey, out var userId) && userId != Guid.Empty)
        {
            var token = tokenIssuer.IssueForUser(userId);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
