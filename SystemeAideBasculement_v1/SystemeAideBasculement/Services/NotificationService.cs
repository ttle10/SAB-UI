using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using SystemeAideBasculement.Models;

namespace SystemeAideBasculement.Services
{
    public class NotificationService : IAsyncDisposable
    {
        private HubConnection? _connection;

        public event Action? OnStateUpdated;

        public event Action<Guid>? OnReminderChanged;

        static public string ReminderChangedMethod => "ReminderChanged";

        public async Task StartAsync(NavigationManager nav)
        {
            if (_connection is not null)
                return;

            _connection = new HubConnectionBuilder()
               .WithUrl(nav.ToAbsoluteUri("/notifications"))
               .WithAutomaticReconnect()
               .Build();

            _connection.On<SabDataNotification>(
                "ProfileUpdated",
                _ =>
                {
                    OnStateUpdated?.Invoke();
                });

            _connection.On<Guid>(ReminderChangedMethod, (initiator) =>
            {
                OnReminderChanged?.Invoke(initiator);
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
