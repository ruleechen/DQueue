using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DQueue.Interfaces;

namespace DQueue
{
    public class QueueConsumer : IDisposable
    {
        private readonly int _threads;
        private readonly QueueProvider _provider;
        private readonly CancellationTokenSource _cts;
        private readonly Dictionary<IQueueProvider, Task<IQueueProvider>> _providerTasks;

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
            _cts = new CancellationTokenSource();
            _providerTasks = new Dictionary<IQueueProvider, Task<IQueueProvider>>();
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

            for (var i = 0; i < _threads; i++)
            {
                var provider = QueueHelpers.CreateProvider(_provider);

                var task = Task.Factory.StartNew<IQueueProvider>(() =>
                {
                    provider.Dequeue<TMessage>(queueName, handler, _cts.Token);

                    return provider;

                }, TaskCreationOptions.LongRunning);

                _providerTasks.Add(provider, task);
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _providerTasks.Clear();
        }
    }
}
