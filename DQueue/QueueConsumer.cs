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
        private class DispatchModel
        {
            public Task ParentTask { get; set; }
            public object Locker { get; set; }
            public List<Task> Tasks { get; set; }
            public CancellationTokenSource CTS { get; set; }
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
        private readonly List<Action<TMessage, IEnumerable<Exception>>> _exceptionHandlers;

        private readonly CancellationTokenSource _cts;
        private readonly Dictionary<int, DispatchModel> _tasks;

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
            _exceptionHandlers = new List<Action<TMessage, IEnumerable<Exception>>>();

            _cts = new CancellationTokenSource();
            _tasks = new Dictionary<int, DispatchModel>();

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
            CheckDisposed();

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

                    _tasks.Add(task.Id, new DispatchModel { ParentTask = task });
                }

                _cts.Token.Register(() =>
                {
                    foreach (var item in _tasks)
                    {
                        var dispatch = item.Value;

                        if (dispatch.CTS != null)
                        {
                            dispatch.CTS.Cancel();
                            dispatch.CTS.Dispose();
                        }

                        if (dispatch.Tasks != null)
                        {
                            dispatch.Tasks.Clear();
                        }
                    }
                });
            }

            return this;
        }

        private void Dispatch(TMessage message, ReceptionContext receptionContext)
        {
            var currentTaskId = Task.CurrentId;
            if (currentTaskId.HasValue && _tasks.ContainsKey(currentTaskId.Value))
            {
                var dispatch = _tasks[currentTaskId.Value];

                dispatch.Locker = new object();
                dispatch.Tasks = new List<Task>();
                dispatch.CTS = new CancellationTokenSource();

                var dispatchContext = new DispatchContext(dispatch.CTS.Token, (sender, status) =>
                {
                    if (status == DispatchStatus.Complete)
                    {
                        if (!dispatch.CTS.IsCancellationRequested)
                        {
                            lock (dispatch.Locker)
                            {
                                if (!dispatch.CTS.IsCancellationRequested)
                                {
                                    dispatch.CTS.Cancel();
                                    dispatch.CTS.Dispose();
                                    dispatch.Tasks.Clear();
                                    FireExceptions(message, sender);
                                    receptionContext.Success();
                                }
                            }
                        }
                    }
                });

                foreach (var handler in _handlers)
                {
                    var task = Task.Factory.StartNew((state) =>
                    {
                        var param = (DispatchState<TMessage>)state;

                        try
                        {
                            param.Handler(param.Message, param.Context);
                        }
                        catch (Exception ex)
                        {
                            // detect cancellation?
                            param.Context.LogException(ex);
                        }
                    },
                    new DispatchState<TMessage>
                    {
                        Message = message,
                        Handler = handler,
                        Context = dispatchContext,
                    },
                    dispatch.CTS.Token,
                    TaskCreationOptions.AttachedToParent,
                    TaskScheduler.Default);

                    dispatch.Tasks.Add(task);
                }

                Task.Factory.ContinueWhenAll(dispatch.Tasks.ToArray(), (t) =>
                {
                    //dispatch.CTS.Cancel();
                    dispatch.CTS.Dispose();
                    dispatch.Tasks.Clear();
                    FireExceptions(message, dispatchContext);
                    receptionContext.Success();
                });
            }
        }

        public QueueConsumer<TMessage> Exceptions(Action<TMessage, IEnumerable<Exception>> handler)
        {
            CheckDisposed();

            if (handler != null)
            {
                _exceptionHandlers.Add(handler);
            }

            return this;
        }

        private void FireExceptions(TMessage message, DispatchContext context)
        {
            if (context.Exceptions.Any())
            {
                foreach (var handler in _exceptionHandlers)
                {
                    try
                    {
                        handler(message, context.Exceptions);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void CheckDisposed()
        {
            if (_cts == null || _cts.IsCancellationRequested)
            {
                throw new InvalidOperationException("Consumer already disposed");
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

            _exceptionHandlers.Clear();
        }
    }
}
