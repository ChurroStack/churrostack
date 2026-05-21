using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;
using static ChurrOS.Api.Commands.Identity.UpsertIdentity;

namespace ChurrOS.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/identities")]
    public class IdentityController : ControllerBase
    {
        private readonly IMediator _mediator;

        public IdentityController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(QueryResult<IdentityItem>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<QueryResult<IdentityItem>>> GetIdentities(CancellationToken cancellationToken,
            [FromQuery] long top = 10, [FromQuery] long skip = 0, [FromQuery] string search = "", [FromQuery] string? type = null, [FromQuery] string? role = null, [FromQuery] string[]? includeNames = default)
        {
            var response = await _mediator.Send(new GetIdentities(top, skip, search, includeNames, type, role), cancellationToken);
            return Ok(response);
        }

        [HttpGet("{identityName}")]
        public async Task<ActionResult<IdentityWithAssignedItem>> GetIdentity([FromRoute] string identityName, CancellationToken cancellationToken)
        {
            var response = await _mediator.Send(new GetIdentity(identityName), cancellationToken);
            return Ok(response);
        }

        [HttpPost]
        public async Task<IActionResult> UpsertIdentity([FromBody] UpsertIdentityBody body, CancellationToken cancellationToken)
        {
            string? ifNoneMatch = null;
            if (Request.Headers.ContainsKey("If-None-Match"))
            {
                ifNoneMatch = Request.Headers["If-None-Match"].ToString().Trim('"');
            }
            var response = await _mediator.Send(new UpsertIdentity(body, ifNoneMatch), cancellationToken);
            return Ok(response);
        }

        [HttpDelete("{identityName}")]
        public async Task<IActionResult> DeleteIdentity([FromRoute] string identityName, CancellationToken cancellationToken)
        {
            await _mediator.Send(new DeleteIdentity(identityName), cancellationToken);
            return Ok(new { identityName });
        }
    }
}
