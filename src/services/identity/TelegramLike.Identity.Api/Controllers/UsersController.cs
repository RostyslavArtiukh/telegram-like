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
public sealed class UsersController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var dto = await mediator.Send(new GetUserByIdQuery(id), cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("by-ids")]
    public async Task<IActionResult> GetUsernamesByIds([FromBody] Guid[] ids, CancellationToken cancellationToken)
    {
        var map = await mediator.Send(new GetUsernamesByIdsQuery(ids), cancellationToken);
        return Ok(map);
    }

    [HttpGet("by-username")]
    public async Task<IActionResult> GetIdByUsername([FromQuery(Name = "u")] string u, CancellationToken cancellationToken)
    {
        var userId = await mediator.Send(new GetUserIdByUsernameQuery(u), cancellationToken);
        return userId is null ? NotFound() : Ok(new { userId });
    }
}
