using System.Net.Http.Headers;

namespace TelegramLike.Web.Services.ServiceAuth;

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
