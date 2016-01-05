using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DQueue.Interfaces;

namespace DQueue.QueueProviders
{
    public class RedisProvider : IQueueProvider
    {
        public void Send(string queueName, object message)
        {
            throw new NotImplementedException();
        }

        public void Receive<TMessage>(string queueName, Action<TMessage, ReceptionContext> handler)
        {
            throw new NotImplementedException();
        }
    }
}
