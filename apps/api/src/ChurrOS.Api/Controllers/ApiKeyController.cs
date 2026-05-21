using ChurrOS.Api.Commands.ApiKeys;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.ApiKey;
using DispatchR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace ChurrOS.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/keys")]
    public class ApiKeyController : ControllerBase
    {
        private readonly IMediator _mediator;
        public ApiKeyController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(QueryResult<ApiKeyItem>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetApiKeys([FromQuery] QueryRequest query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetApiKeys(query), cancellationToken);
            return Ok(result);
        }

        [HttpGet("{id}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(ApiKeyItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetApplication(long id, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetApiKey(id), cancellationToken);
            return Ok(result);
        }

        [HttpPost]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(CreateApiKey.CreateApiKeyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateApiKey([FromBody] CreateApiKey.CreateApiKeyBody body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new CreateApiKey(body), cancellationToken);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteApiKey(long id, CancellationToken cancellationToken)
        {
            await _mediator.Send(new DeleteApiKey(id), cancellationToken);
            return Accepted();
        }
    }
}
