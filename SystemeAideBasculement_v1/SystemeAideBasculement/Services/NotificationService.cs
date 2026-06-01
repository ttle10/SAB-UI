using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using SystemeAideBasculement.Models;

namespace SystemeAideBasculement.Services
{
    public class NotificationService : IAsyncDisposable
    {
        private HubConnection? _hubConnection;

        public event Action? OnStateUpdated;

        public event Action<Guid>? OnReminderChanged;

        private bool _started;

        static public string ReminderChangedMethod => "ReminderChanged";

        public async Task StartAsync(NavigationManager nav)
        {
            // If we've already started once in this circuit, do nothing
            if (_started && _hubConnection is not null &&
                _hubConnection.State != HubConnectionState.Disconnected)
            {
                return;
            }

            if (_hubConnection is null)
            {
                _hubConnection = new HubConnectionBuilder()
                               .WithUrl(nav.ToAbsoluteUri("/notifications"))
                               .WithAutomaticReconnect()
                               .Build();

                _hubConnection.On<SabDataNotification>(
                    "ProfileUpdated",
                    _ =>
                    {
                        OnStateUpdated?.Invoke();
                    });

                _hubConnection.On<Guid>(ReminderChangedMethod, (initiator) =>
                {
                    OnReminderChanged?.Invoke(initiator);
                });
            }

            // Only start if currently disconnected
            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                await _hubConnection.StartAsync();
            }

            _started = true;
        }

        public async ValueTask DisposeAsync()
        {

            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
            }
        }
    }
}
