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
            var queueName = QueueHelpers.GetQueueName<TMessage>();

            this.Send(queueName, message);
        }

        public void Send<TMessage>(string queueName, TMessage message)
        {
            if (string.IsNullOrWhiteSpace(queueName))
            {
                throw new ArgumentNullException("queueName");
            }

            QueueHelpers.GetProvider().Send(queueName, message);
        }
    }
}
