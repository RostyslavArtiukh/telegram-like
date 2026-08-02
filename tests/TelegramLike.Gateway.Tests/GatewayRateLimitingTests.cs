using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using TelegramLike.Gateway;

namespace TelegramLike.Gateway.Tests;

/// <summary>
/// What a request is bucketed by decides whether the limit means anything. Browser users all
/// reach the gateway from one address — the Web BFF proxies them — so bucketing by address
/// would throttle every user together and let one authenticated client in a loop hide inside
/// that shared budget.
/// </summary>
public class GatewayRateLimitingTests
{
    private static string Jwt(object payload)
    {
        static string Segment(string json) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return $"{Segment("{\"alg\":\"HS256\"}")}.{Segment(JsonSerializer.Serialize(payload))}.signature";
    }

    private static HttpContext Request(string path, string? authorization = null, string? remoteIp = "10.0.0.7")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (authorization is not null) context.Request.Headers.Authorization = authorization;
        if (remoteIp is not null) context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        return context;
    }

    [Fact]
    public void TwoUsersBehindTheSameAddress_LandInDifferentBuckets()
    {
        // The case that matters: every browser user arrives from the BFF's address.
        var alice = Request("/chats/my", $"Bearer {Jwt(new { sub = "alice" })}");
        var bob = Request("/chats/my", $"Bearer {Jwt(new { sub = "bob" })}");

        GatewayRateLimiting.CallerKey(alice).Should().Be("user:alice");
        GatewayRateLimiting.CallerKey(bob).Should().Be("user:bob");
    }

    [Fact]
    public void OneUsersRequestsShareABucket_WhicheverServiceTheyAreFor()
    {
        var token = $"Bearer {Jwt(new { sub = "alice" })}";

        GatewayRateLimiting.CallerKey(Request("/messaging/messages", token))
            .Should().Be(GatewayRateLimiting.CallerKey(Request("/presence/heartbeat", token)));
    }

    [Fact]
    public void ARequestWithNoToken_FallsBackToItsSourceAddress()
    {
        // Sign-in and registration. For browser traffic that address is the BFF, so this is an
        // aggregate cap on unauthenticated calls rather than a per-user one.
        GatewayRateLimiting.CallerKey(Request("/identity/auth/login")).Should().Be("anonymous:10.0.0.7");
    }

    [Fact]
    public void ACallerWeCannotIdentifyAtAll_SharesOneBucket_RatherThanGettingAFreeOne()
    {
        GatewayRateLimiting.CallerKey(Request("/chats/my", remoteIp: null))
            .Should().Be("anonymous:unknown");
    }

    [Theory]
    [InlineData("not-a-token")]
    [InlineData("Bearer ")]
    [InlineData("Bearer not.a.jwt")]
    [InlineData("Bearer onlyonesegment")]
    [InlineData("Basic dXNlcjpwYXNz")]
    public void AnUnreadableAuthorizationHeader_DoesNotThrow_AndFallsBackToTheAddress(string header)
    {
        // The header is attacker-controlled and never validated here, so every malformed shape
        // has to degrade to the address rather than fault the pipeline.
        GatewayRateLimiting.CallerKey(Request("/chats/my", header)).Should().Be("anonymous:10.0.0.7");
    }

    [Fact]
    public void ATokenWithoutASubject_FallsBackToTheAddress()
    {
        var token = $"Bearer {Jwt(new { aud = "telegramlike-services" })}";

        GatewayRateLimiting.CallerKey(Request("/chats/my", token)).Should().Be("anonymous:10.0.0.7");
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    [InlineData("/health/live")]
    [InlineData("/metrics")]
    public void ProbesAndScrapesAreExempt(string path)
    {
        // A limiter that starves the healthcheck pulls the instance out of the load balancer
        // for being busy, which is exactly backwards.
        GatewayRateLimiting.IsExempt(path).Should().BeTrue();
    }

    [Theory]
    [InlineData("/chats/my")]
    [InlineData("/messaging/messages")]
    [InlineData("/realtime/hub")]
    public void EverythingProxiedIsLimited(string path)
    {
        GatewayRateLimiting.IsExempt(path).Should().BeFalse();
    }
}
