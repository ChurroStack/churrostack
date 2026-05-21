using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Utils;
using ClosedXML.Excel;
using DispatchR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using static ChurrOS.Api.Commands.Identity.UpsertIdentity;

namespace ChurrOS.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/import")]
    public class ImportController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _dbContext;

        public ImportController(IMediator mediator, ChurrosDbContext dbContext)
        {
            _mediator = mediator;
            _dbContext = dbContext;
        }

        [HttpGet("identities")]
        public async Task<IActionResult> GetImportIdentitiesTemplate(CancellationToken cancellationToken)
        {
            var rawMagerit = Assembly.GetExecutingAssembly().ReadResource("Resources.Import.import-identities-template.xlsx");
            return File(rawMagerit, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "import-identities-template.xlsx");
        }

        [HttpPost("identities")]
        public async Task<IActionResult> ImportIdentities(CancellationToken cancellationToken)
        {
            if (!(Request.Form?.Files?.Count > 0))
            {
                return BadRequest("No file to upload.");
            }

            await _mediator.Send(new EnsureHasRole(IdentityRole.Administrator, _dbContext.IdentityId), cancellationToken);

            var file = Request.Form.Files.First().OpenReadStream();
            using var workBook = new XLWorkbook(file);
            var identities = workBook.Worksheets.First();
            foreach (var row in identities.Rows().Skip(1))
            {
                var name = row.Cell(1).GetString().Trim()!;
                var type = Enum.Parse<IdentityType>(row.Cell(2).GetString()?.Trim() ?? "User", true);
                var displayName = row.Cell(3).GetString()?.Trim();
                var groups = row.Cell(4).GetString()?.Split(';', ',')?.Select(o => o?.Trim()?.Trim()).Where(o => !string.IsNullOrWhiteSpace(o))?.ToArray() ?? [];

                await _mediator.Send(new UpsertIdentity(new UpsertIdentityBody(name, displayName ?? name, type, IdentityRole.User, groups), null), cancellationToken);
            }

            return Ok();
        }
    }
}
