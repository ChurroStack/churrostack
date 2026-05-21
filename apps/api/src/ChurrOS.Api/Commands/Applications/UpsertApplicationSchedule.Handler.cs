using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Jobs;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Quartz;
using System.Text.Json;

namespace ChurrOS.Api.Commands.Applications
{
    public class UpsertApplicationScheduleHandler : IRequestHandler<UpsertApplicationSchedule, ValueTask<ApplicationScheduleItem>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMapper _mapper;
        private readonly IMediator _mediator;
        private readonly ITenantResolver _tenantResolver;
        private readonly ISchedulerFactory _schedulerFactory;

        public UpsertApplicationScheduleHandler(ChurrosDbContext context, IMapper mapper, IMediator mediator, ITenantResolver tenantResolver, ISchedulerFactory schedulerFactory)
        {
            _context = context;
            _mapper = mapper;
            _mediator = mediator;
            _tenantResolver = tenantResolver;
            _schedulerFactory = schedulerFactory;
        }

        public async ValueTask<ApplicationScheduleItem> Handle(UpsertApplicationSchedule request, CancellationToken cancellationToken)
        {
            // Validate input
            ArgumentException.ThrowIfNullOrWhiteSpace(request.Body.Name, nameof(request.Body.Name));
            if (!NamingUtils.IsValidName(request.Body.Name))
            {
                throw new ArgumentException("The provided name is not valid. Only lowercase alphanumeric and _ are allowed.", nameof(request.Body.Name));
            }

            var appRepo = _context.Set<Domain.Application>();
            var schedulerRepo = _context.Set<Domain.ApplicationSchedule>();

            var app = await appRepo
                .AsNoTracking()
                .Include(o => o.Environment)
                .Where(o => o.Name == request.ApplicationName)
                .Select(o => new { o.Id, o.Name, o.Ports, o.AclId, o.EnvironmentId, o.AccountId, EnvironmentAclId = o.Environment.AclId })
                .FirstOrDefaultAsync(cancellationToken);

            if (app == null)
            {
                throw new NotFoundException($"Application with name '{request.ApplicationName}' was not found.");
            }

            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            if (!isAdmin)
            {
                var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Write), cancellationToken);
                if (!identityAcls.ContainsKey(app.AclId) && !identityAcls.ContainsKey(app.EnvironmentAclId))
                    throw new UnauthorizedAccessException("You do not have permission to change this application.");
            }

            if (app?.Ports is null || app.Ports.Length == 0)
                throw new ArgumentException("Application is not deployed or does not have a port defined.");

            var portName = app.Ports[0].Name;

            var scheduleItem = await schedulerRepo
                .Where(o => o.ApplicationId == app.Id && o.Name == request.Body.Name)
                .FirstOrDefaultAsync(cancellationToken);

            var now = DateTimeOffset.UtcNow;
            if (scheduleItem is null)
            {
                schedulerRepo.Add(scheduleItem = new Domain.ApplicationSchedule(_tenantResolver.AccountId, app.Id, request.Body.Name, request.Body.Enabled, request.Body.Description ?? "", request.Body.CronExpression, request.Body.HttpRequest, now, _context.IdentityId, now, _context.IdentityId));
            }
            else
            {
                scheduleItem.Enabled = request.Body.Enabled;
                scheduleItem.Description = request.Body.Description ?? "";
                scheduleItem.CronExpression = request.Body.CronExpression;
                scheduleItem.HttpRequest = request.Body.HttpRequest;
                scheduleItem.ModifiedAt = now;
                scheduleItem.ModifiedById = _context.IdentityId;
            }

            var scheduler = await _schedulerFactory.GetScheduler();
            var appHttpRequest = $"env-{app.EnvironmentId}-app-{app.Id}-{scheduleItem.Name}-httpreq";
            var appHttpRequestJob = JobBuilder.Create<ApplicationHttpRequestJob>()
                .WithIdentity(appHttpRequest)
                .Build();
            if (scheduleItem.Enabled)
            {
                var cronExpression = request.Body.CronExpression;
                var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 5)
                {
                    cronExpression = $"0 {parts[0]} {parts[1]} {parts[2]} {parts[3]} {(parts[4] == "*" ? "?" : parts[4])}";
                }
                else if (parts.Length != 6)
                {
                    throw new ArgumentException("Invalid cron expression.");
                }

                var appHttpRequestTrigger = TriggerBuilder.Create()
                    .WithIdentity(appHttpRequest)
                    .UsingJobData("accountId", app.AccountId.ToString())
                    .UsingJobData("environmentId", app.EnvironmentId.ToString())
                    .UsingJobData("applicationId", app.Id.ToString())
                    .UsingJobData("applicationName", app.Name)
                    .UsingJobData("portName", portName)
                    .UsingJobData("name", scheduleItem.Name)
                    .UsingJobData("httpRequest", JsonSerializer.Serialize(request.Body.HttpRequest, JsonSettings.Value))
                    .WithCronSchedule(cronExpression)
                    .Build();

                try
                {
                    await scheduler.ScheduleJob(appHttpRequestJob, appHttpRequestTrigger);
                }
                catch (JobPersistenceException ex)
                {
                    if (ex.InnerException is ObjectAlreadyExistsException)
                    {
                        await scheduler.DeleteJob(appHttpRequestJob.Key, cancellationToken);
                        await scheduler.ScheduleJob(appHttpRequestJob, appHttpRequestTrigger);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            else
            {
                try
                {
                    await scheduler.DeleteJob(appHttpRequestJob.Key, cancellationToken);
                }
                catch
                {
                    // Job might not exists, ignore any exception
                }
            }
            await _context.SaveChangesAsync(cancellationToken);

            return _mapper.Map<ApplicationScheduleItem>(scheduleItem);
        }
    }
}
