using ChurrOS.Api.Commands.Applications;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Deployment;
using ChurrOS.Api.Models.Dtos.Metrics;
using ChurrOS.Api.Utils;
using DispatchR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using static ChurrOS.Api.Commands.Applications.GetApplications;
using static ChurrOS.Api.Commands.Applications.GetApplicationTraces;
using static ChurrOS.Api.Commands.Applications.GetApplicationUsage;

namespace ChurrOS.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/applications")]
    public class ApplicationController : ControllerBase
    {
        private static JsonSerializerOptions? _jsonSerializerOptions;
        private readonly IMediator _mediator;
        public ApplicationController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(QueryResult<ApplicationSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetApplications([FromQuery] ApplicationQueryRequest query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetApplications(query), cancellationToken);
            return Ok(result);
        }

        [HttpGet("{name}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(ApplicationItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetApplication(string name, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetApplicationByName(name), cancellationToken);
            return Ok(result);
        }

        [HttpPost]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(ApplicationItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateApplication([FromBody] CreateApplication.CreateApplicationBody body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new CreateApplication(body), cancellationToken);
            return Ok(result);
        }

        [HttpPatch("{name}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(ApplicationItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteEnvironment(string name, [FromBody] JsonElement body, CancellationToken cancellationToken = default)
        {
            return Ok(await _mediator.Send(new UpdateApplication(name, body), cancellationToken));
        }

        [HttpDelete("{name}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteApplication(string name, [FromQuery] string? deployment = null, CancellationToken cancellationToken = default)
        {
            await _mediator.Send(new DeleteApplication(name, deployment), cancellationToken);
            return Accepted();
        }

        [HttpPost("{name}/deploy")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeployApplication(string name, [FromQuery] string? deployment = null, CancellationToken cancellationToken = default)
        {
            await _mediator.Send(new DeployApplication(name, deploymentName: deployment), cancellationToken);
            return Accepted();
        }

        [HttpPost("{name}/start")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> StartApplication(string name, [FromQuery] string? deployment = null, CancellationToken cancellationToken = default)
        {
            await _mediator.Send(new StartApplication(name, deployment), cancellationToken);
            return Accepted();
        }

        [HttpPost("{name}/stop")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> StopApplication(string name, [FromQuery] string? deployment = null, CancellationToken cancellationToken = default)
        {
            await _mediator.Send(new StopApplication(name, deployment), cancellationToken);
            return Accepted();
        }

        [HttpGet("{name}/events")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(QueryResult<ApplicationEventItem>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetApplicationEvents(string name, [FromQuery] string? search = null, CancellationToken cancellationToken = default)
        {
            var result = await _mediator.Send(new GetLatestApplicationEvents(name, search: search), cancellationToken);
            return Ok(result);
        }

        [HttpGet("{name}/console/{deploymentName}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task WatchApplicationConsole(string name, string deploymentName, CancellationToken cancellationToken)
        {
            Response.Headers.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            await foreach (var line in _mediator.CreateStream(new WatchApplicationConsole(name, deploymentName), cancellationToken))
            {
                await SendUpdate(line, cancellationToken);
            }

            await SendUpdate<object>(null, cancellationToken);
        }

        [HttpGet("{name}/metrics/{metricName}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(MetricValuesItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetApplicationMetrics(string name, string metricName, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetApplicationMetrics(name, metricName, from, to), cancellationToken);
            return Ok(result);
        }

        [HttpGet("{name}/traces")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(QueryResult<ApplicationTraceItem>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetApplicationTraces(string name, [FromQuery] TracesQueryRequest query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetApplicationTraces(name, query), cancellationToken);
            return Ok(result);
        }

        [HttpGet("{name}/usage/{groupBy}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(QueryResult<ApplicationUsageItem>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetApplicationUsage(string name, string groupBy, [FromQuery] UsageQueryRequest query, CancellationToken cancellationToken)
        {
            query.GroupBy = groupBy;
            var result = await _mediator.Send(new GetApplicationUsage(name, query), cancellationToken);
            return Ok(result);
        }

        [HttpGet("{name}/deployments")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(QueryResult<ApplicationDeploymentItem>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetApplicationDeployments(string name, [FromQuery] QueryRequest query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetApplicationDeployments(name, query), cancellationToken);
            return Ok(result);
        }

        [HttpPost("{name}/deployments")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(DeploymentSummary), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateApplicationDeployment(string name, [FromBody] CreateApplicationDeployment.CreateApplicationDeploymentBody body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new CreateApplicationDeployment(name, body), cancellationToken);
            return Ok(result);
        }

        private async Task SendUpdate<T>(T? @event, CancellationToken cancellationToken)
        {
            if (_jsonSerializerOptions is null)
            {
                _jsonSerializerOptions = new JsonSerializerOptions(JsonSettings.Value)
                {
                    WriteIndented = false
                };

                _jsonSerializerOptions.ApplyDefaultOptions();
            }

            if (@event is not null)
            {
                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes($"data: {JsonSerializer.Serialize(@event, _jsonSerializerOptions).Trim('\r', '\n')}\r\n\r\n"), cancellationToken);
                //await Response.Body.FlushAsync(cancellationToken);
            }
        }
    }
}
