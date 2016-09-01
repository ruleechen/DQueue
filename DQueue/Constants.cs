namespace DQueue
{
    public static class Constants
    {
        public static readonly string JsonField_EnqueueTime = "$EnqueueTime$";
        public static readonly string JsonField_ProcessedTimes = "$ProcessedTimes$";

        public static readonly string Flag_ProcessingQueueName = "-$Processing$";
        public static readonly string Flag_QueueLocker = "-$QueueLocker$";
        public static readonly string Flag_MonitorLocker = "-$MonitorLocker$";

        public static readonly QueueProvider DefaultProvider = QueueProvider.Configured;
        public static readonly int DefaultTimeoutMilliseconds = 1000 * 60 * 5; // 5 minutes
        public static readonly bool RetryOnTimeout = false;
    }
}
