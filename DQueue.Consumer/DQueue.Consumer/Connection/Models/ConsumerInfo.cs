using System;

namespace DQueue.Consumer.Connection.Models
{
    public class ConsumerInfo
    {
        public string QueueName { get; set; }
        public string MaximumThreads { get; set; }
        public TimeSpan? Timeout { get; set; }
    }
}
