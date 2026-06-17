using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TelegramLike.Identity.Application.Queries.GetUserById;
using TelegramLike.Identity.Application.Queries.GetUserIdByUsername;
using TelegramLike.Identity.Application.Queries.GetUsernamesByIds;

namespace TelegramLike.Identity.Api.Controllers;

/// <summary>
/// Authenticated user queries — downstream callers present an Identity-issued JWT
/// (validated against <c>iss=telegramlike-identity</c>). Lookups return 404 when the
/// requested user/username does not resolve.
/// </summary>
[ApiController]
[Authorize]
[Route("users")]
public sealed class UsersController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetUserByIdQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("by-ids")]
    public async Task<IActionResult> GetUsernamesByIds([FromBody] Guid[] ids, CancellationToken ct)
    {
        var map = await _mediator.Send(new GetUsernamesByIdsQuery(ids), ct);
        return Ok(map);
    }

    [HttpGet("by-username")]
    public async Task<IActionResult> GetIdByUsername([FromQuery(Name = "u")] string u, CancellationToken ct)
    {
        var userId = await _mediator.Send(new GetUserIdByUsernameQuery(u), ct);
        return userId is null ? NotFound() : Ok(new { userId });
    }
}
