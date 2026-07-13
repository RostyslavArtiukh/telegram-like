using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TelegramLike.Gateway;
using Yarp.ReverseProxy.Configuration;

namespace TelegramLike.Gateway.Tests;

/// <summary>
/// Route + cluster generation contract for <see cref="GatewayRouting.AddGatewayReverseProxy"/>.
/// Every backend must produce one route (match <c>/&lt;prefix&gt;/{**catch-all}</c>, strip the
/// prefix, forward to a same-named cluster) and one cluster whose destination defaults to the
/// service port but stays overridable via config — the exact keys compose/k8s set.
/// </summary>
public sealed class GatewayRoutingTests
{
    // The six backends the gateway fronts, with their default local-dev addresses.
    // Kept here as the test's own source of truth so a change to the production list
    // (e.g. dropping realtime, renaming a prefix) fails a test rather than passing silently.
    private static readonly (string Prefix, string DefaultAddress)[] Expected =
    [
        ("identity",      "http://localhost:8085"),
        ("notifications", "http://localhost:8081"),
        ("presence",      "http://localhost:8082"),
        ("chats",         "http://localhost:8083"),
        ("messaging",     "http://localhost:8084"),
        ("realtime",      "http://localhost:8086"),
    ];

    private static IProxyConfig BuildConfig(Dictionary<string, string?>? overrides = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(overrides ?? new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddGatewayReverseProxy(configuration);
        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IProxyConfigProvider>().GetConfig();
    }

    [Fact]
    public void GeneratesOneRouteAndClusterPerBackend()
    {
        var config = BuildConfig();

        config.Routes.Select(r => r.RouteId).Should().BeEquivalentTo(Expected.Select(e => e.Prefix));
        config.Clusters.Select(c => c.ClusterId).Should().BeEquivalentTo(Expected.Select(e => e.Prefix));
    }

    [Fact]
    public void IncludesRealtimeBackend()
    {
        // Regression: the gateway fronts 6 backends — the 5 data services plus the realtime
        // SignalR hub (/realtime/hub). An earlier revision listed only the 5 services.
        var config = BuildConfig();

        config.Routes.Should().Contain(r => r.RouteId == "realtime");
        config.Clusters.Should().Contain(c => c.ClusterId == "realtime");
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("notifications")]
    [InlineData("presence")]
    [InlineData("chats")]
    [InlineData("messaging")]
    [InlineData("realtime")]
    public void EachRouteMatchesPrefixPathAndPointsAtSameNamedCluster(string prefix)
    {
        var route = BuildConfig().Routes.Single(r => r.RouteId == prefix);

        route.Match.Path.Should().Be($"/{prefix}/{{**catch-all}}");
        route.ClusterId.Should().Be(prefix);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("notifications")]
    [InlineData("presence")]
    [InlineData("chats")]
    [InlineData("messaging")]
    [InlineData("realtime")]
    public void EachRouteStripsItsPrefix(string prefix)
    {
        var route = BuildConfig().Routes.Single(r => r.RouteId == prefix);

        route.Transforms.Should().NotBeNull();
        route.Transforms!.Should().ContainSingle(t =>
            t.ContainsKey("PathRemovePrefix") && t["PathRemovePrefix"] == $"/{prefix}");
    }

    [Fact]
    public void DestinationFallsBackToDefaultPortWhenNoOverride()
    {
        var config = BuildConfig();

        foreach (var (prefix, defaultAddress) in Expected)
        {
            var cluster = config.Clusters.Single(c => c.ClusterId == prefix);
            cluster.Destinations!.Should().ContainKey("d1");
            cluster.Destinations!["d1"].Address.Should().Be(defaultAddress);
        }
    }

    [Fact]
    public void ConfigOverrideWinsOverDefaultAddress()
    {
        // The exact key shape compose/k8s use to point the gateway at the in-cluster service.
        var overrides = new Dictionary<string, string?>
        {
            ["ReverseProxy:Clusters:chats:Destinations:d1:Address"] = "http://chats:8080",
            ["ReverseProxy:Clusters:messaging:Destinations:d1:Address"] = "http://messaging:8080",
        };

        var config = BuildConfig(overrides);

        config.Clusters.Single(c => c.ClusterId == "chats").Destinations!["d1"].Address
            .Should().Be("http://chats:8080");
        config.Clusters.Single(c => c.ClusterId == "messaging").Destinations!["d1"].Address
            .Should().Be("http://messaging:8080");
        // A backend without an override still falls back to its port.
        config.Clusters.Single(c => c.ClusterId == "identity").Destinations!["d1"].Address
            .Should().Be("http://localhost:8085");
    }
}
