using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DQueue.Interfaces;

namespace DQueue
{
    public class QueueProducer
    {
        private readonly IQueueProvider _provider;

        public QueueProducer()
            : this(QueueProvider.Configured)
        {
        }

        public QueueProducer(QueueProvider provider)
        {
            _provider = QueueHelpers.CreateProvider(provider);
        }

        public QueueProducer Send<TMessage>(TMessage message)
            where TMessage : new()
        {
            var queueName = QueueHelpers.GetQueueName<TMessage>();

            return this.Send(queueName, message);
        }

        public QueueProducer Send(string queueName, object message)
        {
            if (string.IsNullOrWhiteSpace(queueName))
            {
                throw new ArgumentNullException("queueName");
            }

            _provider.Enqueue(queueName, message);

            return this;
        }
    }
}
