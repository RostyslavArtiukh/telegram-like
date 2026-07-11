using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MediatR;
using NSubstitute;
using TelegramLike.Messaging.Tests.Api.Harness;
using TelegramLike.Messaging.Application.Queries;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Tests.Api;

/// <summary>
/// Enum serialization: Emoji and AttachmentType must travel as name strings on the wire
/// (JsonStringEnumConverter is load-bearing — the BFF sends/reads names, never ints).
///
/// Tests verify the full JSON pipeline through the factory:
///   - Request binding: sending the string name binds the correct enum value (covered in
///     MessagingRequestBindingTests; here we focus on the response side).
///   - Response: MessageDto with Emoji/AttachmentType fields serializes as name strings
///     in the JSON body that GET /messages/{id} returns.
///   - An integer-valued enum in a request body must NOT bind (rejects silently or with 400
///     depending on mode — we only assert the name path works, not that integers break).
/// </summary>
public sealed class MessagingEnumSerializationTests(MessagingApiFactory factory)
    : IClassFixture<MessagingApiFactory>
{
    private static readonly Guid SomeMessageId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid SomeChatId    = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid SomeUserId    = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private HttpClient Auth() => factory.CreateAuthenticatedClient();

    // ── GET /messages/{id} response serializes Emoji as name ──────────────

    [Fact]
    public async Task GetMessageById_ReactionEmoji_SerializesAsName()
    {
        var reactionDto = new ReactionDto(SomeUserId, Emoji.Heart, DateTime.UtcNow);
        var messageDto = new MessageDto(
            SomeMessageId, SomeChatId, SomeUserId, "hi",
            Array.Empty<AttachmentDto>(), null, null, null,
            new[] { reactionDto }, false, null, null, null,
            DateTime.UtcNow);

        factory.Mediator
            .Send(Arg.Any<IRequest<MessageDto?>>(), Arg.Any<CancellationToken>())
            .Returns(messageDto);

        var response = await Auth().GetAsync($"/messages/{SomeMessageId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Deserialize as raw JsonDocument to inspect the wire format, not C# enum parsing.
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var reactions = doc.RootElement.GetProperty("reactions");
        reactions.GetArrayLength().Should().Be(1);

        var emojiToken = reactions[0].GetProperty("emoji");
        emojiToken.ValueKind.Should().Be(JsonValueKind.String, "Emoji must serialize as a string name");
        emojiToken.GetString().Should().Be("Heart");
    }

    // ── GET /messages/{id} response serializes AttachmentType as name ─────

    [Fact]
    public async Task GetMessageById_AttachmentType_SerializesAsName()
    {
        var attachmentDto = new AttachmentDto(AttachmentType.Video, "https://example.com/v.mp4", 9876, "v.mp4");
        var messageDto = new MessageDto(
            SomeMessageId, SomeChatId, SomeUserId, null,
            new[] { attachmentDto }, null, null, null,
            Array.Empty<ReactionDto>(), false, null, null, null,
            DateTime.UtcNow);

        factory.Mediator
            .Send(Arg.Any<IRequest<MessageDto?>>(), Arg.Any<CancellationToken>())
            .Returns(messageDto);

        var response = await Auth().GetAsync($"/messages/{SomeMessageId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var attachments = doc.RootElement.GetProperty("attachments");
        attachments.GetArrayLength().Should().Be(1);

        var typeToken = attachments[0].GetProperty("type");
        typeToken.ValueKind.Should().Be(JsonValueKind.String, "AttachmentType must serialize as a string name");
        typeToken.GetString().Should().Be("Video");
    }

    // ── POST /messages: sending AttachmentType as string name binds correctly

    [Fact]
    public async Task SendMessage_AttachmentType_StringName_Binds()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        // The BFF sends "Audio" — it must not be treated as unknown / return 400.
        var body = new StringContent(
            JsonSerializer.Serialize(new
            {
                chatId = SomeChatId,
                text = (string?)null,
                recipients = new[] { SomeUserId },
                isBroadcast = false,
                attachments = new[]
                {
                    new { type = "Audio", url = "https://s.com/a.mp3", sizeBytes = 111, fileName = "a.mp3" }
                }
            }),
            Encoding.UTF8, "application/json");

        var response = await Auth().PostAsync("/messages", body);

        // 201 means the attachment bound correctly (mediator got called).
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ── POST /messages/reactions: sending Emoji as string name binds correctly

    [Fact]
    public async Task AddReaction_EmojiStringName_Binds()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var body = new StringContent(
            JsonSerializer.Serialize(new { emoji = "Angry", actorIsPremium = false }),
            Encoding.UTF8, "application/json");

        var response = await Auth().PostAsync(
            $"/messages/{SomeMessageId}/reactions", body);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── GET /messages/{id} response body property names are camelCase ──────

    [Fact]
    public async Task GetMessageById_ResponseBody_HasCamelCasePropertyNames()
    {
        var messageDto = new MessageDto(
            SomeMessageId, SomeChatId, SomeUserId, "hello",
            Array.Empty<AttachmentDto>(), null, null, null,
            Array.Empty<ReactionDto>(), false, null, null, null,
            DateTime.UtcNow);

        factory.Mediator
            .Send(Arg.Any<IRequest<MessageDto?>>(), Arg.Any<CancellationToken>())
            .Returns(messageDto);

        var response = await Auth().GetAsync($"/messages/{SomeMessageId}");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        // Basic sanity: camelCase key exists (not PascalCase)
        doc.RootElement.TryGetProperty("messageId", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("chatId", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("isRetracted", out _).Should().BeTrue();
    }

    // ── POST /messages: Created response body has messageId property ───────

    [Fact]
    public async Task SendMessage_CreatedBody_HasMessageId()
    {
        var newId = Guid.NewGuid();
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(newId);

        var body = new StringContent(
            JsonSerializer.Serialize(new
            {
                chatId = SomeChatId,
                text = "hi",
                recipients = new[] { SomeUserId },
                isBroadcast = false
            }),
            Encoding.UTF8, "application/json");

        var response = await Auth().PostAsync("/messages", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("messageId", out var idProp).Should().BeTrue();
        idProp.GetGuid().Should().Be(newId);
    }
}
