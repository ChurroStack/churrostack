using ChurrOS.Api.Data;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace ChurrOS.Api.Commands.Environment
{
    public class DeleteEnvironmentJobsHandler : IRequestHandler<DeleteEnvironmentJobs, Task>
    {
        private readonly ChurrosDbContext _dbContext;
        private readonly ISchedulerFactory _schedulerFactory;

        public DeleteEnvironmentJobsHandler(ChurrosDbContext dbContext, ISchedulerFactory schedulerFactory)
        {
            _dbContext = dbContext;
            _schedulerFactory = schedulerFactory;
        }

        public async Task Handle(DeleteEnvironmentJobs request, CancellationToken cancellationToken)
        {
            var scheduler = await _schedulerFactory.GetScheduler();

            var env = await _dbContext.Set<Domain.Environment>()
                .AsNoTracking()
                .Where(o => o.Name == request.Name)
                .Select(o => new { o.Id, o.AccountId })
                .SingleAsync();

            await scheduler.DeleteJob(new JobKey($"env-{env.Id}-events-scrapper"), cancellationToken);
            await scheduler.DeleteJob(new JobKey($"env-{env.Id}-deployment-state-scrapper"), cancellationToken);
            await scheduler.DeleteJob(new JobKey($"env-{env.Id}-metrics-scrapper"), cancellationToken);
        }
    }
}
