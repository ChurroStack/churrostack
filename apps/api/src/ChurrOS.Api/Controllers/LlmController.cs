using ChurrOS.Api.Commands.Llm;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Llm;
using ChurrOS.Api.Models.Dtos.Metrics;
using DispatchR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;
using System.Text.Json;
using static ChurrOS.Api.Commands.Llm.GetLlmDestinationModels;
using static ChurrOS.Api.Commands.Llm.GetLlms;
using static ChurrOS.Api.Commands.Llm.TestLlmDestination;

namespace ChurrOS.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/llms")]
    public class LlmController : ControllerBase
    {
        private readonly IMediator _mediator;

        public LlmController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(QueryResult<LlmSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetLlms([FromQuery] LlmsQueryRequest query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetLlms(query), cancellationToken);
            return Ok(result);
        }

        [HttpGet("{llmId}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(LlmItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetLlm(long llmId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetLlmById(llmId), cancellationToken);
            return Ok(result);
        }

        [HttpPost]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(LlmItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateLlm([FromBody] CreateLlm.CreateLlmBody body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new CreateLlm(body), cancellationToken);
            return Ok(result);
        }

        [HttpPatch("{llmId}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(LlmItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteEnvironment(long llmId, [FromBody] JsonElement body, CancellationToken cancellationToken = default)
        {
            return Ok(await _mediator.Send(new UpdateLlm(llmId, body), cancellationToken));
        }

        [HttpDelete("{llmId}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteLlm(long llmId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new DeleteLlm(llmId), cancellationToken);
            return Accepted();
        }

        [HttpPost("{llmId}/models")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(OaiModel), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDestinationModels(long llmId, [FromBody] GetLlmDestinationModelsBody body, CancellationToken cancellationToken)
        {
            return Ok(await _mediator.Send(new GetLlmDestinationModels(llmId, body), cancellationToken));
        }

        [HttpPost("{llmId}/test")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> TestDestination(long llmId, [FromBody] TestLlmDestinationBody body, CancellationToken cancellationToken)
        {
            await _mediator.Send(new TestLlmDestination(llmId, body), cancellationToken);
            return Ok();
        }

        [HttpGet("{llmId}/metrics/{metricName}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(MetricValuesItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetLlmMetrics(long llmId, string metricName, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetLlmMetrics(llmId, metricName, from, to), cancellationToken);
            return Ok(result);
        }

        [HttpGet("{llmId}/usage/{groupBy}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(QueryResult<LlmUsageItem>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetLlmUsage(long llmId, string groupBy, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] string orderBy = "completions", [FromQuery] string orderDirection = "desc", CancellationToken cancellationToken = default)
        {
            var result = await _mediator.Send(new GetLlmUsage(llmId, groupBy, orderBy, orderDirection, from, to), cancellationToken);
            return Ok(result);
        }
    }
}
