using System.Text;
using System.Text.Json;
using FluentAssertions;
using MediatR;
using NSubstitute;
using TelegramLike.Chats.Api.Tests.Harness;
using TelegramLike.Chats.Application.Commands.ChangeMemberRole;
using TelegramLike.Chats.Application.Commands.CreateBroadcastChannel;
using TelegramLike.Chats.Application.Commands.CreateDirectChat;
using TelegramLike.Chats.Application.Commands.CreateGroupChat;
using TelegramLike.Chats.Application.Commands.KickMember;
using TelegramLike.Chats.Application.Commands.RenameChat;
using TelegramLike.Chats.Application.Commands.TransferOwnership;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Api.Tests;

/// <summary>
/// Request binding: [FromBody] fields reach MediatR with the correct values.
/// Route + body combos (e.g. chatId in route, body fields) are validated together.
/// </summary>
public sealed class ChatsRequestBindingTests(ChatsApiFactory factory)
    : IClassFixture<ChatsApiFactory>
{
    private static readonly Guid CurrentUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ChatId  = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PeerId  = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private HttpClient Auth() => factory.CreateAuthenticatedClient(CurrentUserId);

    private static StringContent Json(object body)
        => new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    // ── CreateDirectChat binds PeerUserId and actor from JWT sub ──────────

    [Fact]
    public async Task CreateDirect_BindsPeerUserId()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        await Auth().PostAsync("/chats/direct", Json(new { peerUserId = PeerId }));

        await factory.Mediator.Received(1).Send(
            Arg.Is<CreateDirectChatCommand>(c =>
                c.InitiatorUserId == CurrentUserId && c.PeerUserId == PeerId),
            Arg.Any<CancellationToken>());
    }

    // ── CreateGroupChat binds Name and actor from JWT sub ─────────────────

    [Fact]
    public async Task CreateGroup_BindsName()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        await Auth().PostAsync("/chats/group", Json(new { name = "Devs Group" }));

        await factory.Mediator.Received(1).Send(
            Arg.Is<CreateGroupChatCommand>(c =>
                c.OwnerUserId == CurrentUserId && c.Name == "Devs Group"),
            Arg.Any<CancellationToken>());
    }

    // ── CreateBroadcastChannel binds Name and actor from JWT sub ──────────

    [Fact]
    public async Task CreateBroadcast_BindsName()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        await Auth().PostAsync("/chats/broadcast", Json(new { name = "News Channel" }));

        await factory.Mediator.Received(1).Send(
            Arg.Is<CreateBroadcastChannelCommand>(c =>
                c.OwnerUserId == CurrentUserId && c.Name == "News Channel"),
            Arg.Any<CancellationToken>());
    }

    // ── RenameChat binds route chatId + body NewName + actor from JWT ──────

    [Fact]
    public async Task Rename_BindsChatIdAndNewName()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var req = new HttpRequestMessage(HttpMethod.Patch, $"/chats/{ChatId}")
        {
            Content = Json(new { newName = "Renamed" }),
            Headers =
            {
                Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer", factory.MintToken(CurrentUserId))
            }
        };
        await factory.CreateClient().SendAsync(req);

        await factory.Mediator.Received(1).Send(
            Arg.Is<RenameChatCommand>(c =>
                c.ChatId == ChatId && c.NewName == "Renamed" && c.RenamedByUserId == CurrentUserId),
            Arg.Any<CancellationToken>());
    }

    // ── KickMember binds both route GUIDs + actor from JWT ────────────────

    [Fact]
    public async Task Kick_BindsChatIdAndMemberToKickUserId()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        await Auth().PostAsync($"/chats/{ChatId}/members/{PeerId}/kick", Json(new { }));

        await factory.Mediator.Received(1).Send(
            Arg.Is<KickMemberCommand>(c =>
                c.ChatId == ChatId && c.MemberToKickUserId == PeerId && c.KickedByUserId == CurrentUserId),
            Arg.Any<CancellationToken>());
    }

    // ── ChangeMemberRole binds NewRole enum as string (JsonStringEnumConverter) ──

    [Fact]
    public async Task ChangeMemberRole_BindsEnumByName()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        // MemberRole serialises as string name because JsonStringEnumConverter
        // is registered globally for Chats (load-bearing wire contract).
        var body = Json(new { newRole = "Admin" });
        await Auth().PostAsync($"/chats/{ChatId}/members/{PeerId}/role", body);

        await factory.Mediator.Received(1).Send(
            Arg.Is<ChangeMemberRoleCommand>(c =>
                c.ChatId == ChatId &&
                c.MemberToChangeUserId == PeerId &&
                c.NewRole == MemberRole.Admin &&
                c.ChangedByUserId == CurrentUserId),
            Arg.Any<CancellationToken>());
    }

    // ── TransferOwnership binds NewOwnerUserId + actor from JWT ───────────

    [Fact]
    public async Task TransferOwnership_BindsNewOwnerId()
    {
        factory.Mediator
            .Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        await Auth().PostAsync(
            $"/chats/{ChatId}/transfer-ownership",
            Json(new { newOwnerUserId = PeerId }));

        await factory.Mediator.Received(1).Send(
            Arg.Is<TransferOwnershipCommand>(c =>
                c.ChatId == ChatId &&
                c.NewOwnerUserId == PeerId &&
                c.CurrentOwnerUserId == CurrentUserId),
            Arg.Any<CancellationToken>());
    }
}
