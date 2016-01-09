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
        private readonly List<Action<TMessage, ReceptionContext>> _handlers;
        private readonly CancellationTokenSource _cts;
        private readonly string _queueName;

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
            _handlers = new List<Action<TMessage, ReceptionContext>>();
            _cts = new CancellationTokenSource();
            _queueName = QueueHelpers.GetQueueName<TMessage>();
            
            if (_threads <= 0)
            {
                throw new ArgumentRangeException("threads")
            }
            
            if (string.IsNullOrWhiteSpace(_queueName))
            {
                throw new ArgumentNullException("queueName");
            }
        }
        
        public string QueueName
        {
            get
            {
                return _queueName;
            }
        }
        
        public string Threads
        {
            get
            {
                return _threads;
            }
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
            if (_cts == null || _cts.IsCancellationRequested)
            {
                throw new InvalidOperationException("Consumer already disposed");
            }
            
            if (handler != null)
            {
                _handlers.Add(handler);
            }
            
            if (_providerTasks.Count != _threads)
            {
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
                        QueueName = _queueName,
                        Provider = provider,
                        Handler = HandlerDispatcher,
                        Token = _cts.Token
                    },
                    _cts.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
    
                    _providerTasks.Add(provider, task);
                }
            }
            
            return this;
        }
        
        private void HandlerDispatcher(TMessage message, ReceptionContext context)
        {
            parara.foreach(_handlers, (handler) =>
            {
                handler(message, context);
            });
        }

        public void Dispose()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
            }

            _providerTasks.Clear();
            _handlers.Clear();
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
