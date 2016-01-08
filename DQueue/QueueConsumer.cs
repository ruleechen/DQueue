using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DQueue.Interfaces;

namespace DQueue
{
    public class QueueConsumer : IDisposable
    {
        private readonly int _threads;
        private readonly QueueProvider _provider;
        private readonly List<Task<IQueueProvider>> _tasks;

        public QueueConsumer()
            : this(QueueProvider.Configured, 1)
        {
        }

        public QueueConsumer(int threads)
            : this(QueueProvider.Configured, threads)
        {
        }

        public QueueConsumer(QueueProvider provider)
            : this(provider, 1)
        {
        }

        public QueueConsumer(QueueProvider provider, int threads)
        {
            _threads = threads;
            _provider = provider;
            _tasks = new List<Task<IQueueProvider>>();
        }

        public void Receive<TMessage>(Action<TMessage> handler)
            where TMessage : new()
        {
            Receive<TMessage>((message, context) =>
            {
                handler(message);
                context.Continue();
            });
        }

        public void Receive<TMessage>(Action<TMessage, ReceptionContext> handler)
            where TMessage : new()
        {
            var queueName = QueueHelpers.GetQueueName<TMessage>();

            if (string.IsNullOrWhiteSpace(queueName))
            {
                throw new ArgumentNullException("queueName");
            }

            this.Dispose();

            for (var i = 0; i < _threads; i++)
            {
                _tasks.Add(Task.Factory.StartNew<IQueueProvider>(() =>
                {
                    var provider = QueueHelpers.GetProvider(_provider);

                    provider.Dequeue<TMessage>(queueName, handler);

                    return provider;

                }));
            }
        }

        public void Dispose()
        {
            foreach (var task in _tasks)
            {
                task.Dispose();
            }

            _tasks.Clear();
        }
    }
}
