using DQueue.Helpers;
using DQueue.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DQueue
{
    public class QueueConsumer<TMessage> : IDisposable
        where TMessage : new()
    {
        #region Helpers
        private class ReceiveState<T>
        {
            public QueueProvider Provider { get; set; }
            public Action<ReceptionContext<T>> Handler { get; set; }
            public ReceptionAssistant Assistant { get; set; }
        }

        private class DispatchState<T>
        {
            public DispatchContext<T> Context { get; set; }
            public Action<DispatchContext<T>> Handler { get; set; }
        }

        private class DispatchModel
        {
            public object Locker { get; set; }
            public List<Task> Tasks { get; set; }
            public CancellationTokenSource CTS { get; set; }
            public void Reset()
            {
                CTS.Cancel();
                CTS.Dispose();
                CTS = null;
                Tasks.Clear();
                Tasks = null;
            }
        }
        #endregion

        private readonly int _threads;
        private readonly int? _timeout;
        private readonly string _queueName;
        private readonly QueueProvider _provider;

        private readonly List<Action<DispatchContext<TMessage>>> _handlers;
        private readonly List<Action<DispatchContext<TMessage>>> _timeoutHandlers;
        private readonly List<Action<DispatchContext<TMessage>>> _completeHandlers;

        private readonly CancellationTokenSource _cts;
        private readonly Dictionary<int, DispatchModel> _tasks;

        public QueueConsumer()
            : this(null, 1, QueueProvider.Configured)
        {
        }

        public QueueConsumer(string queueName)
            : this(queueName, 1, QueueProvider.Configured)
        {
        }

        public QueueConsumer(int threads)
            : this(null, threads, QueueProvider.Configured)
        {
        }

        public QueueConsumer(QueueProvider provider)
            : this(null, 1, provider)
        {
        }

        public QueueConsumer(string queueName, int threads)
            : this(queueName, threads, QueueProvider.Configured)
        {
        }

        public QueueConsumer(string queueName, QueueProvider provider)
            : this(queueName, 1, provider)
        {
        }

        public QueueConsumer(int threads, QueueProvider provider)
            : this(null, threads, provider)
        {
        }

        public QueueConsumer(string queueName, int threads, QueueProvider provider)
        {
            _threads = threads;
            _queueName = queueName ?? QueueNameGenerator.GetQueueName<TMessage>();
            _provider = provider;
            _timeout = ConfigSource.GetAppSettings("ConsumerTimeout").AsNullableInt();

            if (_threads <= 0)
            {
                throw new ArgumentOutOfRangeException("threads");
            }

            if (string.IsNullOrWhiteSpace(_queueName))
            {
                throw new ArgumentNullException("queueName");
            }

            _handlers = new List<Action<DispatchContext<TMessage>>>();
            _timeoutHandlers = new List<Action<DispatchContext<TMessage>>>();
            _completeHandlers = new List<Action<DispatchContext<TMessage>>>();

            _cts = new CancellationTokenSource();
            _tasks = new Dictionary<int, DispatchModel>();
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

        public int? Timeout
        {
            get
            {
                return _timeout;
            }
        }

        public QueueConsumer<TMessage> Receive<TState>(TState state, Action<TState, DispatchContext<TMessage>> handler)
        {
            return Receive((context) =>
            {
                handler(state, context);
            });
        }

        public QueueConsumer<TMessage> Receive(Action<DispatchContext<TMessage>> handler)
        {
            CheckDisposed();

            if (handler != null)
            {
                _handlers.Add(handler);
            }

            if (_tasks.Count < _threads)
            {
                var assistant = new ReceptionAssistant(_threads, _queueName, _cts.Token);

                for (var i = 0; i < _threads; i++)
                {
                    var task = Task.Factory.StartNew((state) =>
                    {
                        var param = (ReceiveState<TMessage>)state;
                        var provider = QueueProviderFactory.CreateProvider(param.Provider);
                        provider.Dequeue<TMessage>(param.Assistant, param.Handler);
                    },
                    new ReceiveState<TMessage>
                    {
                        Provider = _provider,
                        Handler = Dispatch,
                        Assistant = assistant
                    },
                    assistant.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

                    _tasks.Add(task.Id, new DispatchModel());
                }

                assistant.RegisterCancel(1000, true, () =>
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

        private void Dispatch(ReceptionContext<TMessage> receptionContext)
        {
            var currentTaskId = Task.CurrentId;
            if (currentTaskId.HasValue && _tasks.ContainsKey(currentTaskId.Value))
            {
                var dispatch = _tasks[currentTaskId.Value];

                dispatch.Locker = new object();
                dispatch.Tasks = new List<Task>();
                dispatch.CTS = new CancellationTokenSource();

                var dispatchContext = new DispatchContext<TMessage>(
                    receptionContext.Message, dispatch.CTS.Token, (sender, status) =>
                {
                    if (status == DispatchStatus.Complete)
                    {
                        ContinueOnSuccess(receptionContext, sender, dispatch);
                    }
                    else if (status == DispatchStatus.Timeout)
                    {
                        ContinueOnTimeout(receptionContext, sender, dispatch);
                    }
                });

                foreach (var handler in _handlers)
                {
                    var task = Task.Factory.StartNew((state) =>
                    {
                        var param = (DispatchState<TMessage>)state;

                        try
                        {
                            param.Handler(param.Context);
                        }
                        catch (Exception ex)
                        {
                            param.Context.LogException(ex);
                        }
                    },
                    new DispatchState<TMessage>
                    {
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
                    ContinueOnSuccess(receptionContext, dispatchContext, dispatch);
                });
            }
        }

        private void ContinueOnSuccess(ReceptionContext<TMessage> receptionContext, DispatchContext<TMessage> dispatchContext, DispatchModel dispatch)
        {
            if (!dispatch.CTS.IsCancellationRequested)
            {
                lock (dispatch.Locker)
                {
                    if (!dispatch.CTS.IsCancellationRequested)
                    {
                        foreach (var handler in _completeHandlers)
                        {
                            try
                            {
                                handler(dispatchContext);
                            }
                            catch
                            {
                            }
                        }

                        dispatch.Reset();
                        receptionContext.Success();
                    }
                }
            }
        }

        private void ContinueOnTimeout(ReceptionContext<TMessage> receptionContext, DispatchContext<TMessage> dispatchContext, DispatchModel dispatch)
        {
            if (!dispatch.CTS.IsCancellationRequested)
            {
                lock (dispatch.Locker)
                {
                    if (!dispatch.CTS.IsCancellationRequested)
                    {
                        foreach (var handler in _timeoutHandlers)
                        {
                            try
                            {
                                handler(dispatchContext);
                            }
                            catch
                            {
                            }
                        }

                        dispatch.Reset();
                        receptionContext.Timeout();
                    }
                }
            }
        }

        public QueueConsumer<TMessage> OnComplete(Action<DispatchContext<TMessage>> handler)
        {
            CheckDisposed();

            if (handler != null)
            {
                _completeHandlers.Add(handler);
            }

            return this;
        }

        public QueueConsumer<TMessage> OnTimeout(Action<DispatchContext<TMessage>> handler)
        {
            CheckDisposed();

            if (handler != null)
            {
                _timeoutHandlers.Add(handler);
            }

            return this;
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
            _timeoutHandlers.Clear();
            _completeHandlers.Clear();
        }
    }
}
