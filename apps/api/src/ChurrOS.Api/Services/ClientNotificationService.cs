using ChurrOS.Api.Middlewares;
using ChurrOS.Api.Utils.Converters;
using Microsoft.AspNetCore.SignalR;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ChurrOS.Api.Services
{
    public class ClientNotificationService
    {
        [JsonConverter(typeof(JsonStringEnumMemberConverter))]
        public enum NotificationTargetType
        {
            [EnumMember(Value = "application")] Application,
            [EnumMember(Value = "environment")] Environment,
            [EnumMember(Value = "deployment")] Deployment,
            [EnumMember(Value = "account")] Account
        }

        public class NotificationMessage
        {
            public string Name { get; private set; }
            public NotificationTargetType Target { get; private set; }
            public DateTimeOffset Timestamp { get; private set; }

            public NotificationMessage(string name, NotificationTargetType target, DateTimeOffset timestamp)
            {
                Name = name;
                Target = target;
                Timestamp = timestamp;
            }
        }
        private readonly IHubContext<NotificationHub> _hubContext;

        public ClientNotificationService(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotifyChangeAsync(long accountId, string name, NotificationTargetType target, CancellationToken cancellationToken)
        {
            await _hubContext.Clients.Group($"account-{accountId}").SendAsync("onNotification", new NotificationMessage(name, target, DateTimeOffset.Now), cancellationToken);
        }
    }
}
