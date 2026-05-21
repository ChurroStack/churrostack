using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Jobs;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace ChurrOS.Api.Commands.Applications
{
    public class DeleteApplicationScheduleHandler : IRequestHandler<DeleteApplicationSchedule, Task>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMediator _mediator;
        private readonly ISchedulerFactory _schedulerFactory;

        public DeleteApplicationScheduleHandler(ChurrosDbContext context, IMediator mediator, ISchedulerFactory schedulerFactory)
        {
            _context = context;
            _mediator = mediator;
            _schedulerFactory = schedulerFactory;
        }

        public async Task Handle(DeleteApplicationSchedule request, CancellationToken cancellationToken)
        {
            var appRepo = _context.Set<Domain.Application>();
            var schedulerRepo = _context.Set<Domain.ApplicationSchedule>();

            var app = await appRepo
                .AsNoTracking()
                .Include(o => o.Environment)
                .Where(o => o.Name == request.AppName)
                .Select(o => new { o.Id, o.AclId, o.EnvironmentId, EnvironmentAclId = o.Environment.AclId })
                .FirstOrDefaultAsync(cancellationToken);

            if (app == null)
            {
                throw new NotFoundException($"Application with name '{request.AppName}' was not found.");
            }

            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            if (!isAdmin)
            {
                var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Write), cancellationToken);
                if (!identityAcls.ContainsKey(app.AclId) && !identityAcls.ContainsKey(app.EnvironmentAclId))
                    throw new UnauthorizedAccessException("You do not have permission to read this application.");
            }

            var scheduleItem = await schedulerRepo
                .Where(o => o.ApplicationId == app.Id && o.Name == request.Name)
                .FirstOrDefaultAsync(cancellationToken);

            if (scheduleItem != null)
            {
                try
                {
                    var scheduler = await _schedulerFactory.GetScheduler();
                    var appHttpRequest = $"env-{app.EnvironmentId}-app-{app.Id}-{scheduleItem.Name}-httpreq";
                    var appHttpRequestJob = JobBuilder.Create<ApplicationHttpRequestJob>()
                        .WithIdentity(appHttpRequest)
                        .Build();
                    await scheduler.DeleteJob(appHttpRequestJob.Key, cancellationToken);
                }
                catch
                {
                    // Job might not exists, ignore any exception
                }

                schedulerRepo.Remove(scheduleItem);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
