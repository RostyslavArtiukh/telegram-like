using Yarp.ReverseProxy.Configuration;

namespace TelegramLike.Gateway;

/// <summary>
/// Builds the YARP routes + clusters from a single list of backends. Every route is the
/// identical shape — match <c>/&lt;prefix&gt;/**</c>, strip the prefix, forward to a same-named
/// cluster — so we generate them here instead of repeating six near-identical JSON blocks.
/// Prefix routing (rather than natural paths) is required because chats and messaging both
/// serve <c>/chats/*</c> (messaging owns <c>/chats/{chatId}/messages</c>).
/// </summary>
internal static class GatewayRouting
{
    // The backends the gateway fronts, as (prefix, default local-dev address). Adding a
    // service = one line here. Only the address is environment-specific (see below).
    private static readonly (string Prefix, string DefaultAddress)[] Backends =
    [
        ("identity",      "http://localhost:8085"),
        ("notifications", "http://localhost:8081"),
        ("presence",      "http://localhost:8082"),
        ("chats",         "http://localhost:8083"),
        ("messaging",     "http://localhost:8084"),
        ("realtime",      "http://localhost:8086"),
    ];

    public static IReverseProxyBuilder AddGatewayReverseProxy(
        this IServiceCollection services, IConfiguration configuration)
    {
        var routes = Backends.Select(b => new RouteConfig
        {
            RouteId = b.Prefix,
            ClusterId = b.Prefix,
            Match = new RouteMatch { Path = $"/{b.Prefix}/{{**catch-all}}" },
            Transforms = new[]
            {
                new Dictionary<string, string> { ["PathRemovePrefix"] = $"/{b.Prefix}" },
            },
        }).ToArray();

        // Only the destination address is environment-specific, so it stays overridable via
        // ReverseProxy:Clusters:<prefix>:Destinations:d1:Address — the exact keys compose and
        // k8s already set (http://<service>:8080). Absent an override we fall back to the port.
        var clusters = Backends.Select(b => new ClusterConfig
        {
            ClusterId = b.Prefix,
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["d1"] = new()
                {
                    Address = configuration[$"ReverseProxy:Clusters:{b.Prefix}:Destinations:d1:Address"]
                              ?? b.DefaultAddress,
                },
            },
        }).ToArray();

        return services.AddReverseProxy().LoadFromMemory(routes, clusters);
    }
}
