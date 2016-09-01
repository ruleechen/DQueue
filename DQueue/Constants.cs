namespace DQueue
{
    public static class Constants
    {
        public const string JsonField_EnqueueTime = "$EnqueueTime$";
        public const string JsonField_ProcessedTimes = "$ProcessedTimes$";
        public const int DefaultTimeoutMilliseconds = 1000 * 60 * 5; // 5 minutes
    }
}
