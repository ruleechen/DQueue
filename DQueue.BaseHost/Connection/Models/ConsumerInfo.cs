using System;

namespace DQueue.BaseHost.Connection.Models
{
    public class ConsumerInfo
    {
        public string QueueName { get; set; }
        public string MaximumThreads { get; set; }
        public TimeSpan? Timeout { get; set; }
    }
}
