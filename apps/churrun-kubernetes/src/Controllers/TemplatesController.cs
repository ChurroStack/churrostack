using ChurrunKubernetes.Commands.Template;
using ChurrunKubernetes.Models.Dtos;
using ChurrunKubernetes.Models.Dtos.Template;
using DispatchR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace ChurrunKubernetes.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/templates")]
    public class TemplatesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public TemplatesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(QueryResult<TemplateSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ListTemplates([FromQuery] QueryRequest query, CancellationToken cancellationToken = default)
        {
            var result = await _mediator.Send(new GetTemplates(query), cancellationToken);
            return Ok(result);
        }

        [HttpGet("{templateName}")]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(QueryResult<TemplateSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTemplate(string templateName, [FromQuery] string? version = null, CancellationToken cancellationToken = default)
        {
            var result = await _mediator.Send(new GetTemplate(string.IsNullOrWhiteSpace(version) ? templateName : $"{templateName}/{version}"), cancellationToken);
            return Ok(result);
        }

        [HttpPost]
        [Consumes(MediaTypeNames.Text.Plain)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(QueryResult<TemplateSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateTemplate([FromBody] string body, CancellationToken cancellationToken = default)
        {
            var result = await _mediator.Send(new CreateTemplate(body), cancellationToken);
            return Ok(result);
        }
    }
}
