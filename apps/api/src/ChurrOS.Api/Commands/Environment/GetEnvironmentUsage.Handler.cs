using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Environment;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Utils;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Environment
{
    public class GetEnvironmentUsageHandler : IRequestHandler<GetEnvironmentUsage, ValueTask<IList<EnvironmentUsageItem>>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMediator _mediator;

        public GetEnvironmentUsageHandler(ChurrosDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async ValueTask<IList<EnvironmentUsageItem>> Handle(GetEnvironmentUsage request, CancellationToken cancellationToken)
        {
            var environment = await _context.Set<Domain.Environment>()
                .AsNoTracking()
                .Select(e => new { e.Id, e.Name, e.AclId, e.Definition })
                .FirstOrDefaultAsync(e => e.Name == request.EnvironmentName, cancellationToken);

            if (environment == null)
                throw new NotFoundException($"Environment with name '{request.EnvironmentName}' was not found.");

            // The Usage tab is an environment-management view: managers only.
            if (!await _mediator.Send(new IsAdminOrHasAcl(environment.AclId, Permission.Manage), cancellationToken))
                throw new UnauthorizedAccessException("You do not have permission to view environment usage.");

            var apps = await _context.Set<Domain.Application>()
                .AsNoTracking()
                .Where(a => a.EnvironmentId == environment.Id)
                .OrderBy(a => a.Name)
                .Select(a => new { a.Id, a.Name, a.Size })
                .ToListAsync(cancellationToken);

            var appIds = apps.Select(a => a.Id).ToList();
            var recommendations = await _context.Set<Domain.ApplicationSizeRecommendation>()
                .AsNoTracking()
                .Where(r => appIds.Contains(r.ApplicationId))
                .ToDictionaryAsync(r => r.ApplicationId, cancellationToken);

            var sizesCatalog = environment.Definition?.Sizes;

            var result = new List<EnvironmentUsageItem>();
            foreach (var app in apps)
            {
                // Backfill the named-preset hint when the app stored only raw cpu/memory,
                // so the UI can render the same label the size picker does. Build a fresh
                // SizeRequestItem instead of mutating the projection.
                var currentSize = app.Size;
                if (currentSize != null && string.IsNullOrEmpty(currentSize.Hint))
                {
                    var resolvedHint = SizeRecommendation.ResolveHint(sizesCatalog, currentSize);
                    if (resolvedHint != null)
                        currentSize = new SizeRequestItem(resolvedHint, currentSize.Cpu, currentSize.Memory, currentSize.Storage, currentSize.Gpu);
                }

                var item = new EnvironmentUsageItem
                {
                    ApplicationName = app.Name,
                    CurrentSize = currentSize,
                    WindowDays = 7,
                    Direction = SizeRecommendation.NotAnalyzed,
                };

                if (recommendations.TryGetValue(app.Id, out var recommendation))
                {
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
                    item.Direction = SizeRecommendation.GetDirection(currentSize, recommendation.RecommendedSize);
                    item.HasRecommendation = recommendation.RecommendedSize != null
                        && item.Direction != SizeRecommendation.Optimal;
                }

                result.Add(item);
            }

            return result;
        }
    }
}
