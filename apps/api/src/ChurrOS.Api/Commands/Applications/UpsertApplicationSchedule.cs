using ChurrOS.Api.Models.Dtos.Application;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Applications
{
    public class UpsertApplicationSchedule : IRequest<UpsertApplicationSchedule, ValueTask<ApplicationScheduleItem>>
    {
        public class UpsertApplicationScheduleBody
        {
            public string Name { get; private set; }
            public bool Enabled { get; private set; }
            public string? Description { get; private set; }
            public string CronExpression { get; private set; }
            public HttpRequestItem HttpRequest { get; private set; }

            public UpsertApplicationScheduleBody(string name, bool enabled, string? description, string cronExpression, HttpRequestItem httpRequest)
            {
                Name = name;
                Enabled = enabled;
                Description = description;
                CronExpression = cronExpression;
                HttpRequest = httpRequest;
            }
        }

        public string ApplicationName { get; private set; }

        public UpsertApplicationScheduleBody Body { get; private set; }

        public UpsertApplicationSchedule(string applicationName, UpsertApplicationScheduleBody body)
        {
            ApplicationName = applicationName;
            Body = body;
        }
    }
}
