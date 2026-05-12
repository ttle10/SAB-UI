using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SystemeAideBasculement.Hubs;
using SystemeAideBasculement.Models;

namespace SystemeAideBasculement.Services
{
    public sealed class NotificationWorker : BackgroundService
    {
        private readonly INotificationQueue _queue;
        private readonly SabStateCache _cache;
        // Configuration option for batching
        private readonly SabNotificationOptions _options;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<NotificationWorker> _logger;

        public NotificationWorker(
            INotificationQueue queue,
            SabStateCache cache,
            IHubContext<NotificationHub> hubContext,
            IOptions<SabNotificationOptions> options,
            ILogger<NotificationWorker> logger)
        {
            _queue = queue;
            _cache = cache;
            _hubContext = hubContext;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "[SabUI:NotificationWorker:ExecuteAsync]: Notification worker started");

            while (!_cache.IsReady && !stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "[SabUI:NotificationWorker:ExecuteAsync]: Waiting for cache to be ready...");
                await Task.Delay(500, stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var batch = await ReadMultipleAsync(_options.MaxBatchSize, stoppingToken);

                if (batch.Count == 0)
                {
                    _logger.LogTrace(
                        "[SabUI:NotificationWorker:ExecuteAsync]: No notification to process.");
                    await Task.Delay(500, stoppingToken);
                    continue;
                }

                try
                {
                    await Process(batch, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "[SabUI:NotificationWorker:ExecuteAsync]: Error processing notification");
                }
            }
        }

        public async Task<List<IncomingNotification>> ReadMultipleAsync(
            int maxBatchSize,
            CancellationToken stoppingToken)
        {
            var batch = new List<IncomingNotification>();

            var first = await _queue.DequeueAsync(stoppingToken);
            batch.Add(first);

            while (batch.Count < maxBatchSize && _queue.Count > 0)
            {
                var item = await _queue.DequeueAsync(stoppingToken);
                batch.Add(item);
            }
            _logger.LogTrace(
            "[SabUI:NotificationWorker:ReadMultipleAsync]: Batching {Count} notification(s), remaining {Remaining} from queue.",
            batch.Count, _queue.Count);

            return batch;
        }

        private async Task Process(
            List<IncomingNotification> batch,
            CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "[SabUI:NotificationWorker:Process]: Processing {Count} profile notification(s)",
                batch.Count);

            var notifications = batch
                .Select(b => b.Notification)
                .ToList();

            var result = await _cache.UpdateAsync(notifications);

            if (!result.IsEmpty)
            {
                await NotifyAsync(result, stoppingToken);
            }
        }

        private async Task NotifyAsync(
            SabDataNotification notification,
            CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogTrace(
                    "[SabUI:NotificationWorker:NotifyAsync]: Notify clients to refresh.");

                await _hubContext.Clients.All.SendAsync(
                    "ProfileUpdated",
                    notification,
                    stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[SabUI:NotificationWorker:NotifyAsync]: SignalR send failed.");
            }
        }
    }
}