using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using DQueue.Interfaces;

namespace DQueue.QueueProviders
{
    public class RedisProvider : IQueueProvider
    {
        public RedisProvider(string hostName, string userName, string password)
        {
        }

        public void Enqueue(string queueName, object message)
        {
            throw new NotImplementedException();
        }

        public void Dequeue<TMessage>(string queueName, Action<TMessage, ReceptionContext> handler, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
