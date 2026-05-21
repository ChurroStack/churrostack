using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChurrOS.Api.Middlewares
{
    [Authorize]
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            if (Context.GetHttpContext()?.Items?.TryGetValue("AccountId", out var accountId) ?? false)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"account-{accountId}");
            }
            await base.OnConnectedAsync();
        }
    }
}
