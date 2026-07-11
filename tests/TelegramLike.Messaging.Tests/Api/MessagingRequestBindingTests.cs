using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MediatR;
using NSubstitute;
using TelegramLike.Messaging.Tests.Api.Harness;
using TelegramLike.Messaging.Application.Commands.AddReaction;
using TelegramLike.Messaging.Application.Commands.HideMessage;
using TelegramLike.Messaging.Application.Commands.MarkMessageAsRead;
using TelegramLike.Messaging.Application.Commands.RemoveReaction;
using TelegramLike.Messaging.Application.Commands.RetractMessage;
using TelegramLike.Messaging.Application.Commands.SendMessage;
using TelegramLike.Messaging.Application.Queries;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Tests.Api;

/// <summary>
/// Request binding: BFF-enriched fields (recipients, isBroadcast, userIsPremium,
/// retractedByModerator) bind from JSON body into the exact command properties.
/// Enum values (Emoji, AttachmentType) round-trip as name strings, not integers.
/// </summary>
public sealed class MessagingRequestBindingTests(MessagingApiFactory factory)
    : IClassFixture<MessagingApiFactory>
{
    private static readonly Guid CurrentUserId   = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ChatId    = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid MessageId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid Recipient1 = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid Recipient2 = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private HttpClient Auth() => factory.CreateAuthenticatedClient(CurrentUserId);

    private static StringContent Json(object body)
        => new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    // ── SendMessage: BFF-enriched recipients + isBroadcast ────────────────

    [Fact]
    public async Task SendMessage_binds_BFF_enriched_Recipients()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        var body = Json(new
        {
            chatId = ChatId,
            text = "Hello",
            recipients = new[] { Recipient1, Recipient2 },
            isBroadcast = true
        });
        await Auth().PostAsync("/messages", body);

        await factory.Mediator.Received(1).Send(
            Arg.Is<SendMessageCommand>(c =>
                c.ChatId == ChatId &&
                c.AuthorId == CurrentUserId &&
                c.Text == "Hello" &&
                c.IsBroadcast == true &&
                c.Recipients.Count == 2 &&
                c.Recipients.Contains(Recipient1) &&
                c.Recipients.Contains(Recipient2)),
            Arg.Any<CancellationToken>());
    }

    // ── SendMessage: AttachmentType binds as enum name string ─────────────

    [Fact]
    public async Task SendMessage_AttachmentType_binds_as_enum_name()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        var body = Json(new
        {
            chatId = ChatId,
            text = "photo",
            recipients = new[] { Recipient1 },
            isBroadcast = false,
            attachments = new[]
            {
                new { type = "Image", url = "https://example.com/img.png", sizeBytes = 12345, fileName = "img.png" }
            }
        });
        await Auth().PostAsync("/messages", body);

        await factory.Mediator.Received(1).Send(
            Arg.Is<SendMessageCommand>(c =>
                c.Attachments != null &&
                c.Attachments.Count == 1 &&
                c.Attachments[0].Type == AttachmentType.Image &&
                c.Attachments[0].Url == "https://example.com/img.png" &&
                c.Attachments[0].SizeBytes == 12345 &&
                c.Attachments[0].FileName == "img.png"),
            Arg.Any<CancellationToken>());
    }

    // ── AddReaction: Emoji binds as enum name string, userIsPremium binds ─

    [Fact]
    public async Task AddReaction_binds_Emoji_and_UserIsPremium()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        // The BFF sends the emoji name ("Heart") and userIsPremium flag.
        var body = Json(new { emoji = "Heart", userIsPremium = true });
        await Auth().PostAsync($"/messages/{MessageId}/reactions", body);

        await factory.Mediator.Received(1).Send(
            Arg.Is<AddReactionCommand>(c =>
                c.MessageId == MessageId &&
                c.UserId == CurrentUserId &&
                c.Emoji == Emoji.Heart &&
                c.UserIsPremium == true),
            Arg.Any<CancellationToken>());
    }

    // ── RemoveReaction: emoji name in route segment reaches mediator ───────

    [Fact]
    public async Task RemoveReaction_valid_Emoji_binds_enum()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        await Auth().DeleteAsync($"/messages/{MessageId}/reactions/Fire");

        await factory.Mediator.Received(1).Send(
            Arg.Is<RemoveReactionCommand>(c =>
                c.MessageId == MessageId &&
                c.UserId == CurrentUserId &&
                c.Emoji == Emoji.Fire),
            Arg.Any<CancellationToken>());
    }

    // ── RetractMessage: retractedByModerator (BFF-enriched) binds ─────────────

    [Fact]
    public async Task Retract_binds_RetractedByModerator()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var body = Json(new { retractedByModerator = true });
        await Auth().PostAsync($"/messages/{MessageId}/retract", body);

        await factory.Mediator.Received(1).Send(
            Arg.Is<RetractMessageCommand>(c =>
                c.MessageId == MessageId &&
                c.RetractedByUserId == CurrentUserId &&
                c.RetractedByModerator == true),
            Arg.Any<CancellationToken>());
    }

    // ── MarkAsRead: isBroadcast (BFF-enriched) binds ──────────────────────

    [Fact]
    public async Task MarkAsRead_binds_IsBroadcast()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var body = Json(new { isBroadcast = true });
        await Auth().PostAsync($"/messages/{MessageId}/read", body);

        await factory.Mediator.Received(1).Send(
            Arg.Is<MarkMessageAsReadCommand>(c =>
                c.MessageId == MessageId &&
                c.ReaderUserId == CurrentUserId &&
                c.IsBroadcast == true),
            Arg.Any<CancellationToken>());
    }

    // ── HideMessage: binds messageId from route + actor from JWT ──────────

    [Fact]
    public async Task HideMessage_binds_MessageId_and_actor()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        await Auth().PostAsync($"/messages/{MessageId}/hide", Json(new { }));

        await factory.Mediator.Received(1).Send(
            Arg.Is<HideMessageCommand>(c =>
                c.MessageId == MessageId && c.UserId == CurrentUserId),
            Arg.Any<CancellationToken>());
    }
}
