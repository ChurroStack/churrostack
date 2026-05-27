using ChurrOS.Api.Commands.Tags;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace ChurrOS.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/tags")]
    public class TagsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public TagsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // Tag-list endpoints expose only the permissions the pickers use; any other value is a 400.
        private static Permission ValidatePermission(Permission? permission)
        {
            var value = permission ?? Permission.Read;
            if (value != Permission.Read && value != Permission.Execute)
                throw new ArgumentException("Only 'read' and 'execute' are supported for tag listing.");
            return value;
        }

        [HttpGet("applications")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetApplicationTags([FromQuery] Permission? permission, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetApplicationTags(ValidatePermission(permission)), cancellationToken);
            return Ok(result);
        }

        [HttpGet("environments")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetEnvironmentTags([FromQuery] Permission? permission, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetEnvironmentTags(ValidatePermission(permission)), cancellationToken);
            return Ok(result);
        }
    }
}
