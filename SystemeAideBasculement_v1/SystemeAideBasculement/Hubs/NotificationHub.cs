using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SystemeAideBasculement.Models;
using SystemeAideBasculement.Services;

namespace SystemeAideBasculement.Hubs
{
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;

        public NotificationHub(ILogger<NotificationHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("[SabUI:NotificationHub:OnConnectedAsync]: Client connected: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (exception != null)
            {
                _logger.LogWarning(exception, "[SabUI:NotificationHub:OnDisconnectedAsync]: Client disconnected with error: {ConnectionId}", Context.ConnectionId);
            }
            else
            {
                _logger.LogInformation("[SabUI:NotificationHub:OnDisconnectedAsync]: Client disconnected: {ConnectionId}", Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }


        public async Task BroadcastProfileUpdates(
                   List<ProfileConnectionNotificationModel> notifications)
        {
            _logger.LogInformation(
                "[SabUI:NotificationHub:Broadcasting {Count} profile updates",
                notifications.Count);

            await Clients.All.SendAsync(
                "ProfileUpdated",
                notifications);
        }

    }
}