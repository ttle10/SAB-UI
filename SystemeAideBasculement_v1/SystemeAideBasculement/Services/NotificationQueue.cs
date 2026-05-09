using System.Threading.Channels;

namespace SystemeAideBasculement.Services
{
    public interface INotificationQueue
    {
        ValueTask EnqueueAsync(
            IncomingNotification message,
            CancellationToken ct = default);

        ValueTask<IncomingNotification> DequeueAsync(
            CancellationToken ct);

        bool TryEnqueue(IncomingNotification message);

        int Count { get; }

        void Complete();
    }

    public sealed class NotificationQueue : INotificationQueue
    {
        private readonly Channel<IncomingNotification> _channel;
        private int _count;

        public NotificationQueue()
        {
            _channel = Channel.CreateBounded<IncomingNotification>(
                new BoundedChannelOptions(500)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = false
                });
        }

        public async ValueTask EnqueueAsync(
            IncomingNotification notification,
            CancellationToken ct = default)
        {
            await _channel.Writer.WriteAsync(notification, ct);
            Interlocked.Increment(ref _count);
        }

        public bool TryEnqueue(IncomingNotification notification)
        {
            var success = _channel.Writer.TryWrite(notification);
            if (success)
            {
                Interlocked.Increment(ref _count);
            }

            return success;
        }

        public async ValueTask<IncomingNotification> DequeueAsync(CancellationToken ct)
        {
            var item = await _channel.Reader.ReadAsync(ct);
            Interlocked.Decrement(ref _count);
            return item;
        }

        public int Count => _count;

        public void Complete()
        {
            _channel.Writer.Complete();
        }
    }
}