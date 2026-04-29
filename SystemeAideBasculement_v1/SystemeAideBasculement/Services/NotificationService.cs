namespace SystemeAideBasculement.Services
{
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.SignalR.Client;
    using SystemeAideBasculement.Models;

    public class NotificationService : IAsyncDisposable
    {
        private HubConnection? _connection;

        public event Action<List<ProfileConnectionNotificationModel>>? OnNotifications;

        public async Task StartAsync(NavigationManager nav, SabStateCache cache)
        {
            if (_connection is not null)
                return;

            _connection = new HubConnectionBuilder()
               .WithUrl(nav.ToAbsoluteUri("/notifications"))
               .WithAutomaticReconnect()
               .Build();

            _connection.On<List<ProfileConnectionNotificationModel>>(
                "ProfileUpdated",
                notifications =>
                {
                    foreach (var n in notifications)
                        cache.Update(n);

                    OnNotifications?.Invoke(notifications);
                });

            await _connection.StartAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_connection != null)
            {
                await _connection.DisposeAsync();
            }
        }
    }
}
