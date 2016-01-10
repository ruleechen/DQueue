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
        #region Helpers
        private class TaskModel
        {
            public Task ReceiveTask { get; set; }
            public List<Task> DispatchTasks { get; set; }
            public CancellationTokenSource DispatchCTS { get; set; }
        }

        private class DispatchState<T>
        {
            public T Message { get; set; }
            public DispatchContext Context { get; set; }
            public Action<T, DispatchContext> Handler { get; set; }
        }

        private class ReceiveState<T>
        {
            public string QueueName { get; set; }
            public IQueueProvider Provider { get; set; }
            public Action<T, ReceptionContext> Handler { get; set; }
            public CancellationToken Token { get; set; }
        }
        #endregion

        private readonly int _threads;
        private readonly QueueProvider _provider;
        private readonly string _queueName;
        private readonly List<Action<TMessage, DispatchContext>> _handlers;

        private readonly List<TaskModel> _tasks;
        private readonly CancellationTokenSource _cts;
        private readonly object _dispatchLocker;

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
            _queueName = QueueHelpers.GetQueueName<TMessage>();
            _handlers = new List<Action<TMessage, DispatchContext>>();

            _tasks = new List<TaskModel>();
            _cts = new CancellationTokenSource();
            _dispatchLocker = new object();

            if (_threads <= 0)
            {
                throw new ArgumentOutOfRangeException("threads");
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

        public int Threads
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
            });
        }

        public QueueConsumer<TMessage> Receive(Action<TMessage, DispatchContext> handler)
        {
            if (_cts == null || _cts.IsCancellationRequested)
            {
                throw new InvalidOperationException("Consumer already disposed");
            }

            if (handler != null)
            {
                _handlers.Add(handler);
            }

            if (_tasks.Count < _threads)
            {
                for (var i = 0; i < _threads; i++)
                {
                    var provider = QueueHelpers.CreateProvider(_provider);

                    var task = Task.Factory.StartNew((state) =>
                    {
                        var param = (ReceiveState<TMessage>)state;
                        param.Provider.Dequeue<TMessage>(
                            param.QueueName,
                            param.Handler,
                            param.Token);
                    },
                    new ReceiveState<TMessage>
                    {
                        QueueName = _queueName,
                        Provider = provider,
                        Handler = Dispatch,
                        Token = _cts.Token
                    },
                    _cts.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

                    _tasks.Add(new TaskModel { ReceiveTask = task });
                }

                _cts.Token.Register(() =>
                {
                    foreach (var item in _tasks)
                    {
                        if (item.DispatchCTS != null)
                        {
                            item.DispatchCTS.Cancel();
                            item.DispatchCTS.Dispose();
                        }

                        if (item.DispatchTasks != null)
                        {
                            item.DispatchTasks.Clear();
                        }
                    }
                });
            }

            return this;
        }

        private void Dispatch(TMessage message, ReceptionContext receptionContext)
        {
            var currentTaskId = Task.CurrentId;
            if (currentTaskId.HasValue)
            {
                var model = _tasks.FirstOrDefault(x => x.ReceiveTask.Id == currentTaskId.Value);
                if (model != null)
                {
                    lock (_dispatchLocker)
                    {
                        model.DispatchTasks = new List<Task>();
                        model.DispatchCTS = new CancellationTokenSource();

                        var dispatchContext = new DispatchContext(model.DispatchCTS.Token, (status) =>
                        {
                            if (status == DispatchStatus.Complete)
                            {
                                model.DispatchCTS.Cancel();
                                model.DispatchCTS.Dispose();
                                model.DispatchTasks.Clear();
                                receptionContext.Continue();
                            }
                        });

                        foreach (var handler in _handlers)
                        {
                            var task = Task.Factory.StartNew((state) =>
                            {
                                var param = (DispatchState<TMessage>)state;
                                param.Handler(param.Message, param.Context);
                            },
                            new DispatchState<TMessage>
                            {
                                Message = message,
                                Handler = handler,
                                Context = dispatchContext,
                            },
                            model.DispatchCTS.Token,
                            TaskCreationOptions.AttachedToParent,
                            TaskScheduler.Default);

                            model.DispatchTasks.Add(task);
                        }

                        Task.Factory.ContinueWhenAll(model.DispatchTasks.ToArray(), (t) =>
                        {
                            model.DispatchCTS = null;
                            model.DispatchTasks.Clear();
                            receptionContext.Continue();
                        });
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
            }

            _tasks.Clear();

            _handlers.Clear();
        }
    }
}
