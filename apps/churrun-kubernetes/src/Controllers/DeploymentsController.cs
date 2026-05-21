using ChurrunKubernetes.Commands.Deployment;
using ChurrunKubernetes.Models.Dtos;
using ChurrunKubernetes.Models.Dtos.Deployment;
using DispatchR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace ChurrunKubernetes.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/deployments")]
    public class DeploymentsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DeploymentsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(DeploymentSummary), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeployTemplateAsync([FromBody] CreateDeployment.CreateDeploymentBody body, [FromQuery] bool dry = false, CancellationToken cancellationToken = default)
        {
            var result = await _mediator.Send(new CreateDeployment(body, dry), cancellationToken);
            return Ok(result);
        }

        [HttpPost("{appId}/start")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> StartAsync(string appId, [FromQuery] int? replicas = null, [FromQuery] byte[]? hash = null, CancellationToken cancellationToken = default)
        {
            await _mediator.Send(new StartDeployment(appId, replicas ?? 1, hash), cancellationToken);
            return Accepted();
        }


        [HttpPost("{appId}/stop")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> StopAsync(string appId, [FromQuery] byte[]? hash = null, CancellationToken cancellationToken = default)
        {
            await _mediator.Send(new StopDeployment(appId, hash), cancellationToken);
            return Accepted();
        }

        [HttpDelete("{appId}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAsync(string appId, [FromQuery] byte[]? hash, CancellationToken cancellationToken = default)
        {
            await _mediator.Send(new DeleteDeployment(appId, hash), cancellationToken);
            return Accepted();
        }
    }
}
