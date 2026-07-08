using System.Diagnostics;
using FluentAssertions;

namespace TelegramLike.Gateway.Tests;

public class RedactAccessTokenProcessorTests
{
    private static Activity NewActivity() => new Activity("test-activity").Start();

    [Theory]
    [InlineData("url.query")]
    [InlineData("url.full")]
    [InlineData("http.url")]
    public void Scrubs_access_token_from_known_url_tags(string tag)
    {
        using var activity = NewActivity();
        activity.SetTag(tag, "https://gw/realtime/hub?access_token=super-secret-jwt&other=1");
        var processor = new RedactAccessTokenProcessor();

        processor.OnEnd(activity);

        var value = (string)activity.GetTagItem(tag)!;
        value.Should().NotContain("super-secret-jwt");
        value.Should().Contain("access_token=REDACTED");
        value.Should().Contain("other=1", "non-token query params must survive the redaction");
    }

    [Fact]
    public void Token_at_end_of_query_string_is_fully_redacted()
    {
        using var activity = NewActivity();
        activity.SetTag("url.query", "?access_token=super-secret-jwt");
        var processor = new RedactAccessTokenProcessor();

        processor.OnEnd(activity);

        var value = (string)activity.GetTagItem("url.query")!;
        value.Should().Be("?access_token=REDACTED");
    }

    [Fact]
    public void Tag_without_access_token_is_left_untouched()
    {
        using var activity = NewActivity();
        activity.SetTag("url.query", "?foo=bar&baz=qux");
        var processor = new RedactAccessTokenProcessor();

        processor.OnEnd(activity);

        activity.GetTagItem("url.query").Should().Be("?foo=bar&baz=qux");
    }

    [Fact]
    public void Unrelated_tags_are_never_touched()
    {
        using var activity = NewActivity();
        activity.SetTag("http.method", "GET");
        activity.SetTag("url.query", "?access_token=leak-me");
        var processor = new RedactAccessTokenProcessor();

        processor.OnEnd(activity);

        activity.GetTagItem("http.method").Should().Be("GET");
    }

    [Fact]
    public void Missing_tags_do_not_throw()
    {
        using var activity = NewActivity();
        var processor = new RedactAccessTokenProcessor();

        var act = () => processor.OnEnd(activity);

        act.Should().NotThrow();
    }

    [Fact]
    public void Non_string_tag_value_is_left_untouched()
    {
        using var activity = NewActivity();
        activity.SetTag("url.query", 12345); // not a string
        var processor = new RedactAccessTokenProcessor();

        var act = () => processor.OnEnd(activity);

        act.Should().NotThrow();
        activity.GetTagItem("url.query").Should().Be(12345);
    }
}
