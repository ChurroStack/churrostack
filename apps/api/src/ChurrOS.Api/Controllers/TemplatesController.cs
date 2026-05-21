using ChurrOS.Api.Commands.Template;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Template;
using DispatchR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;
using static ChurrOS.Api.Commands.Template.GetTemplates;

namespace ChurrOS.Api.Controllers
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
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(QueryResult<TemplateSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetTemplates([FromQuery] TemplateQueryRequest query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetTemplates(query), cancellationToken);
            return Ok(result);
        }

        [HttpGet("{name}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(TemplateItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTemplate(string name, CancellationToken cancellationToken = default)
        {
            var parts = name.Split(':', 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
            {
                return BadRequest(new ErrorMessage("Invalid template name format. Expected format is 'name:target'."));
            }
            var result = await _mediator.Send(new GetTemplateByName(parts[0], parts[1]), cancellationToken);
            return Ok(result);
        }

        [HttpPost]
        [Consumes(MediaTypeNames.Text.Plain)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(TemplateItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateTemplate([FromBody] string content, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new CreateTemplate(content), cancellationToken);
            return Ok(result);
        }

        [HttpPut("{name}")]
        [Consumes(MediaTypeNames.Text.Plain)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(TemplateItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateTemplate(string name, [FromBody] string content, CancellationToken cancellationToken = default)
        {
            var parts = name.Split(':', 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
            {
                return BadRequest(new ErrorMessage("Invalid template name format. Expected format is 'name:target'."));
            }
            var result = await _mediator.Send(new UpdateTemplate(parts[0], parts[1], content), cancellationToken);
            return Ok(result);
        }

        [HttpDelete("{name}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteTemplate(string name, CancellationToken cancellationToken)
        {
            var parts = name.Split(':', 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
            {
                return BadRequest(new ErrorMessage("Invalid template name format. Expected format is 'name:target'."));
            }
            await _mediator.Send(new DeleteTemplate(parts[0], parts[1]), cancellationToken);
            return Accepted();
        }
    }
}
