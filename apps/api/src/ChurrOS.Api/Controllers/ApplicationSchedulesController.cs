using ChurrOS.Api.Commands.Applications;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Application;
using DispatchR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace ChurrOS.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/applications/{appName}/schedules")]
    public class ApplicationSchedulesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ApplicationSchedulesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(QueryResult<ApplicationScheduleItem>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetApplications(string appName, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetApplicationSchedules(appName), cancellationToken);
            return Ok(result);
        }

        [HttpPost]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(ApplicationScheduleItem), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateApplication(string appName, [FromBody] UpsertApplicationSchedule.UpsertApplicationScheduleBody body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new UpsertApplicationSchedule(appName, body), cancellationToken);
            return Ok(result);
        }

        [HttpDelete("{name}")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteApplication(string appName, string name, CancellationToken cancellationToken = default)
        {
            await _mediator.Send(new DeleteApplicationSchedule(appName, name), cancellationToken);
            return Accepted();
        }
    }
}
