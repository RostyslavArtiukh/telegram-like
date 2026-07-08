using System.Diagnostics;
using System.Text.RegularExpressions;
using OpenTelemetry;

namespace TelegramLike.Gateway;

/// <summary>
/// The realtime hub's SignalR clients send the access JWT as <c>?access_token=</c>, which
/// the gateway proxies (so the query is on the gateway's own span too). Scrub it from any
/// captured URL tag so a short-lived token can't linger in Jaeger.
/// </summary>
internal sealed partial class RedactAccessTokenProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        Redact(activity, "url.query");
        Redact(activity, "url.full");
        Redact(activity, "http.url");
    }

    private static void Redact(Activity activity, string tag)
    {
        if (activity.GetTagItem(tag) is not string value || !value.Contains("access_token=", StringComparison.Ordinal))
            return;
        activity.SetTag(tag, AccessTokenRegex().Replace(value, "access_token=REDACTED"));
    }

    [GeneratedRegex("access_token=[^&]*")]
    private static partial Regex AccessTokenRegex();
}
