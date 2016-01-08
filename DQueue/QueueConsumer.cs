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
        private readonly List<IQueueProvider> _providers;
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
            _providers = new List<IQueueProvider>();
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
                var provider = QueueHelpers.GetProvider(_provider);

                _providers.Add(provider);

                _tasks.Add(Task.Factory.StartNew<IQueueProvider>(() =>
                {
                    provider.Dequeue<TMessage>(queueName, handler);

                    return provider;

                }, TaskCreationOptions.LongRunning));
            }
        }

        public void Dispose()
        {
            foreach (var item in _providers)
            {
                item.RequestStop();
            }

            _providers.Clear();

            foreach (var item in _tasks)
            {
                item.Dispose();
            }

            _tasks.Clear();
        }
    }
}
