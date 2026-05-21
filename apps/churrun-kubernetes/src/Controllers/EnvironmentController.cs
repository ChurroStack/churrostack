using ChurrunKubernetes.Commands.Environment;
using ChurrunKubernetes.Models.Dtos.Environment;
using DispatchR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace ChurrunKubernetes.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/environment")]
    public class EnvironmentController : ControllerBase
    {
        private readonly IMediator _mediator;

        public EnvironmentController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(EnvironmentDefinition), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetEnvironmentInfoAsync(CancellationToken cancellationToken = default)
        {
            return Ok(await _mediator.Send(new GetEnvironment(), cancellationToken));
        }
    }
}
