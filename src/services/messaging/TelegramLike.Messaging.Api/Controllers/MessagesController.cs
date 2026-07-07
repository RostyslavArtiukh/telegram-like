using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TelegramLike.Messaging.Api.Contracts;
using TelegramLike.Messaging.Application.Commands.HideMessage;
using TelegramLike.Messaging.Application.Commands.RetractMessage;
using TelegramLike.Messaging.Application.Commands.SendMessage;
using TelegramLike.Messaging.Application.Queries.GetChatMessages;
using TelegramLike.Messaging.Application.Queries.GetMessageById;

namespace TelegramLike.Messaging.Api.Controllers;

/// <summary>
/// Message lifecycle + reads: send, fetch by id, retract, hide, and the keyset-paged chat
/// message listing. Reactions and read-receipts live in their own controllers since they
/// mutate embedded sub-resources. Thin: all logic stays in the MediatR handlers; domain
/// exceptions are mapped to status codes by <see cref="Filters.DomainExceptionFilter"/>.
/// </summary>
[Authorize]
public sealed class MessagesController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public MessagesController(IMediator mediator) => _mediator = mediator;

    [HttpPost("/messages")]
    public async Task<IActionResult> Send([FromBody] SendMessageRequest body, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var attachments = body.Attachments?
            .Select(a => new SendMessageAttachment(a.Type, a.Url, a.SizeBytes, a.FileName))
            .ToList();

        var id = await _mediator.Send(new SendMessageCommand(
            body.MessageId,
            body.ChatId,
            userId,
            body.Text,
            body.Recipients,
            body.IsBroadcast,
            attachments,
            body.ReplyToMessageId,
            body.ForwardOriginalMessageId,
            body.ForwardOriginalChatId), cancellationToken);

        return Created($"/messages/{id}", new MessageCreatedResponse(id));
    }

    [HttpGet("/messages/{messageId:guid}")]
    public async Task<IActionResult> GetById(Guid messageId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _mediator.Send(new GetMessageByIdQuery(messageId, userId), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("/messages/{messageId:guid}/retract")]
    public async Task<IActionResult> Retract(
        Guid messageId, [FromBody] RetractMessageRequest body, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await _mediator.Send(new RetractMessageCommand(messageId, userId, body.ActorIsModerator), cancellationToken);
        return NoContent();
    }

    [HttpPost("/messages/{messageId:guid}/hide")]
    public async Task<IActionResult> Hide(Guid messageId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await _mediator.Send(new HideMessageCommand(messageId, userId), cancellationToken);
        return NoContent();
    }

    [HttpGet("/chats/{chatId:guid}/messages")]
    public async Task<IActionResult> GetChatMessages(
        Guid chatId,
        [FromQuery] DateTime? before,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _mediator.Send(
            new GetChatMessagesQuery(chatId, userId, before, pageSize ?? 50), cancellationToken);
        return Ok(result);
    }
}
