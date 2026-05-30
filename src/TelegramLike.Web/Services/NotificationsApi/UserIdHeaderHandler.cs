namespace TelegramLike.Web.Services.NotificationsApi;

/// Attaches the current authenticated user's ID as `X-User-Id` header so the downstream
/// Notifications service can authorize the call. Web (BFF) is the trust boundary — service
/// itself runs inside the docker network and accepts the header at face value.
internal sealed class UserIdHeaderHandler(CurrentUserAccessor currentUser) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var userId = await currentUser.GetUserIdAsync();
        if (userId.HasValue)
            request.Headers.Add("X-User-Id", userId.Value.ToString());

        return await base.SendAsync(request, cancellationToken);
    }
}
