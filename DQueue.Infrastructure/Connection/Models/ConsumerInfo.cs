using System;

namespace DQueue.Infrastructure.Connection.Models
{
    public class ConsumerInfo
    {
        public string QueueName { get; set; }
        public string MaximumThreads { get; set; }
        public TimeSpan? Timeout { get; set; }
    }
}
