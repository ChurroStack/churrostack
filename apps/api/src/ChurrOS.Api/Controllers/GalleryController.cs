using ChurrOS.Api.Commands.Gallery;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Gallery;
using DispatchR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace ChurrOS.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/gallery")]
    public class GalleryController : ControllerBase
    {
        private readonly IMediator _mediator;
        public GalleryController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("applications")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(QueryResult<GalleryAppSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetApplications([FromQuery] GetGalleryApps.GalleryAppsQueryRequest query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetGalleryApps(query), cancellationToken);
            return Ok(result);
        }

        [HttpGet("llms")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(QueryResult<GalleryLlmSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetLlms([FromQuery] QueryRequest query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetGalleryLlms(query), cancellationToken);
            return Ok(result);
        }
    }
}
