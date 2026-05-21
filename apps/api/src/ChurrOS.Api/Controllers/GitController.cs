using ChurrOS.Api.Commands.Git;
using ChurrOS.Api.Models.Dtos;
using DispatchR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;
using static ChurrOS.Api.Commands.Git.GetGitRepositoryInfo;

namespace ChurrOS.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/git")]
    public class GitController : ControllerBase
    {
        private readonly IMediator _mediator;

        public GitController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("check")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(GitRepositoryItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateApplication([FromBody] GetGitRepositoryInfoBody body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetGitRepositoryInfo(body), cancellationToken);
            return Ok(result);
        }
    }
}
