using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Utils;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Applications
{
    public class GetApplicationSizeRecommendationHandler : IRequestHandler<GetApplicationSizeRecommendation, ValueTask<ApplicationSizeRecommendationItem>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMediator _mediator;

        public GetApplicationSizeRecommendationHandler(ChurrosDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async ValueTask<ApplicationSizeRecommendationItem> Handle(GetApplicationSizeRecommendation request, CancellationToken cancellationToken)
        {
            var app = await _context.Set<Domain.Application>()
                .AsNoTracking()
                .Select(o => new { o.Id, o.Name, o.AclId, o.Size, EnvironmentAclId = o.Environment!.AclId })
                .FirstOrDefaultAsync(o => o.Name == request.AppName, cancellationToken);

            if (app == null)
                throw new NotFoundException($"Application with name '{request.AppName}' was not found.");

            // Editors and managers (Write or Manage) can see the recommendation.
            var authorized = await _mediator.Send(new IsAdminOrHasAcl(app.AclId, Permission.Write), cancellationToken)
                || await _mediator.Send(new IsAdminOrHasAcl(app.EnvironmentAclId, Permission.Write), cancellationToken);
            if (!authorized)
                throw new UnauthorizedAccessException("You do not have permission to view the size recommendation.");

            var item = new ApplicationSizeRecommendationItem
            {
                ApplicationName = app.Name,
                CurrentSize = app.Size,
                WindowDays = 7,
                Direction = SizeRecommendation.NotAnalyzed,
            };

            var recommendation = await _context.Set<Domain.ApplicationSizeRecommendation>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ApplicationId == app.Id, cancellationToken);

            if (recommendation == null)
                return item;

            item.RecommendedSize = recommendation.RecommendedSize;
            item.CpuAvg = recommendation.CpuAvg;
            item.CpuMax = recommendation.CpuMax;
            item.CpuP95 = recommendation.CpuP95;
            item.MemoryAvg = recommendation.MemoryAvg;
            item.MemoryMax = recommendation.MemoryMax;
            item.MemoryP95 = recommendation.MemoryP95;
            item.SampleCount = recommendation.SampleCount;
            item.WindowDays = recommendation.WindowDays;
            item.ComputedAt = recommendation.ComputedAt;
            item.Direction = SizeRecommendation.GetDirection(app.Size, recommendation.RecommendedSize);
            item.HasRecommendation = recommendation.RecommendedSize != null
                && item.Direction != SizeRecommendation.Optimal;

            return item;
        }
    }
}
