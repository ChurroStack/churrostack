using ChurrOS.Api.Commands.Applications;
using ChurrOS.Api.Commands.Environment;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Environment;
using DispatchR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace ChurrOS.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/environments")]
    public class EnvironmentsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public EnvironmentsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(QueryResult<EnvironmentSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetEnvironments([FromQuery] QueryRequest query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetEnvironments(query), cancellationToken);
            return Ok(result);
        }

        [HttpGet("{name}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(EnvironmentItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEnvironment(string name, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetEnvironmentByName(name), cancellationToken);
            return Ok(result);
        }

        [HttpPost]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(EnvironmentItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateEnvironment([FromBody] CreateEnvironment.CreateEnvironmentBody body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new CreateEnvironment(body), cancellationToken);
            return Ok(result);
        }

        [HttpPatch("{name}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(EnvironmentItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteEnvironment(string name, [FromBody] UpdateEnvironment.UpdateEnvironmentBody body, [FromQuery] bool validate = false, CancellationToken cancellationToken = default)
        {
            return Ok(await _mediator.Send(new UpdateEnvironment(name, body, validate), cancellationToken));
        }

        [HttpDelete("{name}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteEnvironment(string name, CancellationToken cancellationToken)
        {
            await _mediator.Send(new DeleteEnvironment(name), cancellationToken);
            return Accepted();
        }

        [HttpPost("{name}/rotate")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(EnvironmentKeysItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RotateEnvironmentKeys(string name, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new RotateEnvironmentKeys(name), cancellationToken);
            return Ok(result);
        }

        [HttpPost("{name}/connect")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConnectEnvironment(string name, CancellationToken cancellationToken)
        {
            await _mediator.Send(new ConnectEnvironment(name), cancellationToken);
            return Ok();
        }

        [HttpGet("{name}/usage")]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(IList<EnvironmentUsageItem>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEnvironmentUsage(string name, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetEnvironmentUsage(name), cancellationToken);
            return Ok(result);
        }

        [HttpGet("{name}/totals")]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(EnvironmentTotalsItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEnvironmentTotals(string name, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetEnvironmentTotals(name), cancellationToken);
            return Ok(result);
        }

        [HttpPost("{name}/analyze-usage")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(AnalyzeUsageResultItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AnalyzeEnvironmentUsage(string name, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new AnalyzeApplicationUsage(environmentName: name), cancellationToken);
            return Ok(result);
        }
    }
}
