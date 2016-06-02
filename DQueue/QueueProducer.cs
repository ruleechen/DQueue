using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DQueue.Helpers;
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
            IgnoreHash = false;
            _provider = QueueProviderFactory.CreateProvider(provider);
        }

        public bool IgnoreHash
        {
            get;
            set;
        }

        public QueueProducer Send<TMessage>(TMessage message)
            where TMessage : new()
        {
            var queueName = QueueNameGenerator.GetQueueName<TMessage>();

            return this.Send(queueName, message);
        }

        public QueueProducer Send(string queueName, object message)
        {
            if (string.IsNullOrWhiteSpace(queueName))
            {
                throw new ArgumentNullException("queueName");
            }

            _provider.IgnoreHash = IgnoreHash;
            _provider.Enqueue(queueName, message);

            return this;
        }
    }
}
