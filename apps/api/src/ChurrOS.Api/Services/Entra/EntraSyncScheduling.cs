using ChurrOS.Api.Jobs;
using ChurrOS.Api.Models.Dtos.IdentitySync;
using Quartz;

namespace ChurrOS.Api.Services.Entra
{
    /// <summary>Quartz plumbing for the per-tenant Entra sync: one durable job (so a manual run works with the schedule off) plus an optional cron trigger.</summary>
    public static class EntraSyncScheduling
    {
        private const string Group = "entra-sync";

        public static JobKey JobKey(long accountId) => new(EntraSyncConfigStore.JobName(accountId), Group);

        public static TriggerKey CronTriggerKey(long accountId) => new(EntraSyncConfigStore.CronTriggerName(accountId), Group);

        /// <summary>
        /// Converts a 5-field cron (minute hour day-of-month month day-of-week) to Quartz's 6-field form
        /// (same convention as UpsertApplicationSchedule), setting '?' on whichever day field is unrestricted.
        /// </summary>
        public static string ToQuartzCron(string cronExpression)
        {
            var parts = (cronExpression ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string quartz;
            if (parts.Length == 5)
            {
                var dayOfMonth = parts[2];
                var dayOfWeek = parts[4];
                if (dayOfWeek == "*") dayOfWeek = "?";
                else if (dayOfMonth == "*") dayOfMonth = "?";
                quartz = $"0 {parts[0]} {parts[1]} {dayOfMonth} {parts[3]} {dayOfWeek}";
            }
            else if (parts.Length == 6)
            {
                quartz = string.Join(' ', parts);
            }
            else
            {
                throw new ArgumentException("Invalid cron expression: expected 5 fields (minute hour day-of-month month day-of-week).");
            }

            if (!CronExpression.IsValidExpression(quartz))
                throw new ArgumentException($"Invalid cron expression '{cronExpression}'.");
            return quartz;
        }

        public static async Task EnsureJobAsync(IScheduler scheduler, long accountId, CancellationToken cancellationToken)
        {
            var key = JobKey(accountId);
            if (await scheduler.CheckExists(key, cancellationToken)) return;

            var job = JobBuilder.Create<EntraSyncJob>()
                .WithIdentity(key)
                .UsingJobData("accountId", accountId.ToString())
                .StoreDurably()
                .Build();
            await scheduler.AddJob(job, replace: true, cancellationToken);
        }

        public static async Task ApplyScheduleAsync(IScheduler scheduler, long accountId, EntraSyncConfig config, CancellationToken cancellationToken)
        {
            await EnsureJobAsync(scheduler, accountId, cancellationToken);
            var triggerKey = CronTriggerKey(accountId);

            if (!config.Enabled)
            {
                await scheduler.UnscheduleJob(triggerKey, cancellationToken);
                return;
            }

            var trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .ForJob(JobKey(accountId))
                .UsingJobData("trigger", "schedule")
                .WithCronSchedule(ToQuartzCron(config.CronExpression), x => x.InTimeZone(TimeZoneInfo.Utc))
                .Build();

            if (await scheduler.RescheduleJob(triggerKey, trigger, cancellationToken) is null)
                await scheduler.ScheduleJob(trigger, cancellationToken);
        }

        public static async Task TriggerNowAsync(IScheduler scheduler, long accountId, CancellationToken cancellationToken)
        {
            await EnsureJobAsync(scheduler, accountId, cancellationToken);
            await scheduler.TriggerJob(JobKey(accountId), new JobDataMap { ["trigger"] = "manual" }, cancellationToken);
        }
    }
}
