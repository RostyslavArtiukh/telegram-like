using System.Diagnostics;
using System.Text.RegularExpressions;
using OpenTelemetry;

namespace TelegramLike.Realtime.Api.Observability;

/// <summary>
/// SignalR-over-WebSocket clients send the access JWT as <c>?access_token=</c> (browsers
/// can't set headers on the upgrade). ASP.NET Core instrumentation would otherwise record
/// that URL on the hub span, so a short-lived token could linger in Jaeger. This scrubs it
/// from any captured URL tag on span end.
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
