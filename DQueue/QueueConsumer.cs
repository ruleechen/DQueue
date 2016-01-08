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
        private readonly Dictionary<IQueueProvider, Task> _providerTasks;

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
            _providerTasks = new Dictionary<IQueueProvider, Task>();
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

                var task = Task.Factory.StartNew((state) =>
                {
                    var link = (ThreadState<TMessage>)state;
                    link.Provider.Dequeue<TMessage>(
                        link.QueueName,
                        link.Handler,
                        link.Token);
                },
                new ThreadState<TMessage>
                {
                    QueueName = queueName,
                    Provider = provider,
                    Handler = handler,
                    Token = _cts.Token
                },
                _cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

                _providerTasks.Add(provider, task);
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _providerTasks.Clear();
        }

        private class ThreadState<TMessage>
        {
            public string QueueName { get; set; }
            public IQueueProvider Provider { get; set; }
            public Action<TMessage, ReceptionContext> Handler { get; set; }
            public CancellationToken Token { get; set; }
        }
    }
}
