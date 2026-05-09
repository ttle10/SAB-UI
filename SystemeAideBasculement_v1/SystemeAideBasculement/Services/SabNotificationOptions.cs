namespace SystemeAideBasculement.Services
{
    public sealed class SabNotificationOptions
    {
        /// <summary>
        /// Debounce window for profile notifications
        /// </summary>
        public TimeSpan DebounceInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Max number of queued notifications before forced processing (safety)
        /// </summary>
        public int MaxBatchSize { get; set; } = 1000;
    }
}
