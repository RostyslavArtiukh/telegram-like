using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TelegramLike.Chats.Api.Contracts;
using TelegramLike.Chats.Application.Commands.ChangeMemberRole;
using TelegramLike.Chats.Application.Commands.JoinChat;
using TelegramLike.Chats.Application.Commands.KickMember;
using TelegramLike.Chats.Application.Commands.LeaveChat;
using TelegramLike.Chats.Application.Commands.TransferOwnership;
using TelegramLike.Chats.Application.Queries.GetChatMembers;

namespace TelegramLike.Chats.Api.Controllers;

[Route("chats")]
[Authorize]
public sealed class ChatMembersController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public ChatMembersController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{chatId:guid}/members")]
    public async Task<IActionResult> GetChatMembers(Guid chatId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _mediator.Send(new GetChatMembersQuery(chatId, userId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{chatId:guid}/join")]
    public async Task<IActionResult> Join(Guid chatId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await _mediator.Send(new JoinChatCommand(chatId, userId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{chatId:guid}/leave")]
    public async Task<IActionResult> Leave(Guid chatId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await _mediator.Send(new LeaveChatCommand(chatId, userId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{chatId:guid}/members/{targetUserId:guid}/kick")]
    public async Task<IActionResult> KickMember(Guid chatId, Guid targetUserId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var actorId)) return Unauthorized();
        await _mediator.Send(new KickMemberCommand(chatId, targetUserId, actorId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{chatId:guid}/members/{targetUserId:guid}/role")]
    public async Task<IActionResult> ChangeMemberRole(
        Guid chatId, Guid targetUserId, [FromBody] ChangeMemberRoleRequest body, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var actorId)) return Unauthorized();
        await _mediator.Send(new ChangeMemberRoleCommand(chatId, targetUserId, body.NewRole, actorId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{chatId:guid}/transfer-ownership")]
    public async Task<IActionResult> TransferOwnership(
        Guid chatId, [FromBody] TransferOwnershipRequest body, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var actorId)) return Unauthorized();
        await _mediator.Send(new TransferOwnershipCommand(chatId, body.NewOwnerUserId, actorId), cancellationToken);
        return NoContent();
    }
}
