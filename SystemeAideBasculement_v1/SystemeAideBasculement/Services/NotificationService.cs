namespace SystemeAideBasculement.Services
{
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.SignalR.Client;
    using SystemeAideBasculement.Models;

    public class NotificationService : IAsyncDisposable
    {
        private HubConnection? _connection;
        private readonly SabStateCache _cache;

        public event Action? OnStateUpdated;

        public NotificationService(SabStateCache cache)
        {
            _cache = cache;
            _cache.OnStateChanged += HandleCacheStateChanged;
        }

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

            await _connection.StartAsync();
        }

        public async ValueTask DisposeAsync()
        {
            _cache.OnStateChanged -= HandleCacheStateChanged;

            if (_connection != null)
            {
                await _connection.DisposeAsync();
            }
        }

        private void HandleCacheStateChanged()
        {
            OnStateUpdated?.Invoke();
        }
    }
}
