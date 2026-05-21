using ChurrOS.Api.Commands.Account;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Account;
using DispatchR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace ChurrOS.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/account")]
    public class AccountController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AccountController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(AccountSummary), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetEnvironments(CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetAccount(), cancellationToken);
            return Ok(result);
        }
    }
}
