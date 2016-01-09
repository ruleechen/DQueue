using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DQueue.Interfaces;

namespace DQueue
{
    public class QueueConsumer<TMessage> : IDisposable
        where TMessage : new()
    {
        private readonly int _threads;
        private readonly QueueProvider _provider;
        private readonly Dictionary<IQueueProvider, Task> _providerTasks;
        private CancellationTokenSource _cts;

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
            _providerTasks = new Dictionary<IQueueProvider, Task>();
        }

        public QueueConsumer<TMessage> Receive(Action<TMessage> handler)
        {
            return Receive((message, context) =>
            {
                handler(message);
                context.Continue();
            });
        }

        public QueueConsumer<TMessage> Receive(Action<TMessage, ReceptionContext> handler)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                throw new InvalidOperationException("Consumer is allowed only one handler");
            }

            _cts = new CancellationTokenSource();

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

            return this;
        }

        public void Dispose()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
            }

            _providerTasks.Clear();
        }

        private class ThreadState<T>
        {
            public string QueueName { get; set; }
            public IQueueProvider Provider { get; set; }
            public Action<T, ReceptionContext> Handler { get; set; }
            public CancellationToken Token { get; set; }
        }
    }
}
