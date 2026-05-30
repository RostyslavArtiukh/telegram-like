namespace TelegramLike.Web.Services.NotificationsApi;

public sealed class ServiceAuthOptions
{
    public string JwtSecret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int TokenLifetimeSeconds { get; set; } = 300;
}
