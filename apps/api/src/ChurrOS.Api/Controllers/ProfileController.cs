using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChurrOS.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/profile")]
    public class ProfileController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ProfileController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<ActionResult<ProfileItem>> GetProfile(CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(User?.Identity?.Name, "identity");
            var response = await _mediator.Send(new GetUserProfile(User.Identity.Name.ToLowerInvariant().Trim(), (User.Identity as ClaimsIdentity)?.Claims?.ToArray() ?? []), cancellationToken);
            return Ok(response);
        }
    }
}
