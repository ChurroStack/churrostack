using ChurrOS.Api.Data;
using ChurrOS.Api.Jobs;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace ChurrOS.Api.Commands.Environment
{
    public class UpdateEnvironmentJobsHandler : IRequestHandler<UpdateEnvironmentJobs, Task>
    {
        private readonly ChurrosDbContext _dbContext;
        private readonly ISchedulerFactory _schedulerFactory;

        public UpdateEnvironmentJobsHandler(ChurrosDbContext dbContext, ISchedulerFactory schedulerFactory)
        {
            _dbContext = dbContext;
            _schedulerFactory = schedulerFactory;
        }

        public async Task Handle(UpdateEnvironmentJobs request, CancellationToken cancellationToken)
        {
            var scheduler = await _schedulerFactory.GetScheduler();

            var env = await _dbContext.Set<Domain.Environment>()
                .AsNoTracking()
                .Where(o => o.Name == request.Name)
                .Select(o => new { o.Id, o.AccountId })
                .SingleAsync();

            await ScheduleGenericEventScrappingJob(scheduler, (env.Id, env.AccountId), cancellationToken);
            await ScheduleDeploymentStateChangeScrappingJob(scheduler, (env.Id, env.AccountId), cancellationToken);
            await ScheduleMetricsScrappingJob(scheduler, (env.Id, env.AccountId), cancellationToken);
        }

        private static async Task ScheduleGenericEventScrappingJob(IScheduler scheduler, (long EnvironmentId, long AccountId) env, CancellationToken cancellationToken)
        {
            var scrapeIdentity = $"env-{env.EnvironmentId}-events-scrapper";
            var scrapeTrigger = TriggerBuilder.Create()
                .WithIdentity(scrapeIdentity)
                .UsingJobData("accountId", env.AccountId.ToString())
                .UsingJobData("environmentId", env.EnvironmentId.ToString())
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithInterval(TimeSpan.FromSeconds(5))
                    .RepeatForever()
                    .WithMisfireHandlingInstructionNowWithExistingCount())
                .Build();
            var scrapeJob = JobBuilder.Create<ScrapeGenericEventsJob>()
                .WithIdentity(scrapeIdentity)
                .Build();
            await ScheduleJobAsync(scheduler, scrapeTrigger, scrapeJob, cancellationToken);
        }

        private static async Task ScheduleDeploymentStateChangeScrappingJob(IScheduler scheduler, (long EnvironmentId, long AccountId) env, CancellationToken cancellationToken)
        {
            var scrapeIdentity = $"env-{env.EnvironmentId}-deployment-state-scrapper";
            var scrapeTrigger = TriggerBuilder.Create()
                .WithIdentity(scrapeIdentity)
                .UsingJobData("accountId", env.AccountId.ToString())
                .UsingJobData("environmentId", env.EnvironmentId.ToString())
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithInterval(TimeSpan.FromSeconds(5))
                    .RepeatForever()
                    .WithMisfireHandlingInstructionNowWithExistingCount())
                .Build();
            var scrapeJob = JobBuilder.Create<ScrapeDeploymentStateJob>()
                .WithIdentity(scrapeIdentity)
                .Build();
            await ScheduleJobAsync(scheduler, scrapeTrigger, scrapeJob, cancellationToken);
        }

        private static async Task ScheduleMetricsScrappingJob(IScheduler scheduler, (long EnvironmentId, long AccountId) env, CancellationToken cancellationToken)
        {
            var scrapeIdentity = $"env-{env.EnvironmentId}-metrics-scrapper";
            var scrapeTrigger = TriggerBuilder.Create()
                .WithIdentity(scrapeIdentity)
                .UsingJobData("accountId", env.AccountId.ToString())
                .UsingJobData("environmentId", env.EnvironmentId.ToString())
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithInterval(TimeSpan.FromSeconds(30))
                    .RepeatForever()
                    .WithMisfireHandlingInstructionNowWithExistingCount())
                .Build();
            var scrapeJob = JobBuilder.Create<ScrapeMetricsJob>()
                .WithIdentity(scrapeIdentity)
                .Build();
            await ScheduleJobAsync(scheduler, scrapeTrigger, scrapeJob, cancellationToken);
        }

        private static async Task ScheduleJobAsync(IScheduler scheduler, ITrigger scrapeEventsTrigger, IJobDetail scrapeEventsJob, CancellationToken cancellationToken)
        {
            try
            {
                await scheduler.ScheduleJob(scrapeEventsJob, scrapeEventsTrigger);
            }
            catch (JobPersistenceException ex)
            {
                if (ex.InnerException is ObjectAlreadyExistsException)
                {
                    await scheduler.DeleteJob(scrapeEventsJob.Key, cancellationToken);
                    await scheduler.ScheduleJob(scrapeEventsJob, scrapeEventsTrigger);
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
