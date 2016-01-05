using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DQueue.Interfaces;

namespace DQueue
{
    public class QueueProducer
    {
        public void Send<TMessage>(TMessage message)
            where TMessage : new()
        {
            Send(null, message);
        }

        public void Send<TMessage>(string queueName, TMessage message)
            where TMessage : new()
        {
            if (string.IsNullOrWhiteSpace(queueName))
            {
                queueName = QueueHelpers.GetQueueName<TMessage>();
            }

            Send(queueName, (object)message);
        }

        public void Send(string queueName, object message)
        {
            QueueHelpers.GetProvider().Send(queueName, message);
        }
    }
}
