using ChurrOS.Api.Commands.IdentitySync;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.IdentitySync;
using DispatchR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurrOS.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/identities/sync/entra")]
    public class IdentitySyncController : ControllerBase
    {
        private readonly IMediator _mediator;

        public IdentitySyncController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("config")]
        [ProducesResponseType(typeof(EntraSyncConfigItem), StatusCodes.Status200OK)]
        public async Task<ActionResult<EntraSyncConfigItem>> GetConfig(CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetEntraSyncConfig(), cancellationToken));

        [HttpPut("config")]
        [ProducesResponseType(typeof(EntraSyncConfigItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<EntraSyncConfigItem>> UpsertConfig([FromBody] EntraSyncConfig body, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new UpsertEntraSyncConfig(body), cancellationToken));

        [HttpGet("status")]
        [ProducesResponseType(typeof(EntraSyncStatus), StatusCodes.Status200OK)]
        public async Task<ActionResult<EntraSyncStatus>> GetStatus(CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetEntraSyncStatus(), cancellationToken));

        [HttpPost("start")]
        [ProducesResponseType(typeof(EntraSyncStatus), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<EntraSyncStatus>> Start(CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new StartEntraSync(), cancellationToken));

        [HttpPost("stop")]
        [ProducesResponseType(typeof(EntraSyncStatus), StatusCodes.Status200OK)]
        public async Task<ActionResult<EntraSyncStatus>> Stop(CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new StopEntraSync(), cancellationToken));
    }
}
