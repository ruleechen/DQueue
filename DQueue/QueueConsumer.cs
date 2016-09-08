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
        private class DispatchModel : IDisposable
        {
            public object Locker { get; private set; }
            public List<Task> Tasks { get; private set; }
            public CancellationTokenSource CTS { get; private set; }

            public DispatchModel()
            {
                Locker = new object();
                Tasks = new List<Task>();
                CTS = new CancellationTokenSource();
            }

            public void Dispose()
            {
                if (CTS != null && !CTS.IsCancellationRequested)
                {
                    CTS.Cancel();
                    CTS.Dispose();
                }

                if (Tasks != null)
                {
                    Tasks.Clear();
                }
            }
        }
        #endregion

        private readonly QueueProvider _provider;
        private readonly CancellationTokenSource _cts;

        private readonly List<Action<DispatchContext<TMessage>>> _handlers;
        private readonly List<Action<DispatchContext<TMessage>>> _timeoutHandlers;
        private readonly List<Action<DispatchContext<TMessage>>> _completeHandlers;

        public string QueueName { get; set; }
        public int MaxThreads { get; set; }
        public TimeSpan? Timeout { get; set; }

        public QueueConsumer()
            : this(null, 1)
        {
        }

        public QueueConsumer(string queueName)
            : this(queueName, 1)
        {
        }

        public QueueConsumer(int maxThreads)
            : this(null, maxThreads)
        {
        }

        public QueueConsumer(string queueName, int maxThreads)
        {
            MaxThreads = maxThreads;
            QueueName = queueName ?? QueueNameGenerator.GetQueueName<TMessage>();
            Timeout = ConfigSource.GetAppSettings("ConsumerTimeout").AsNullableTimeSpan();

            if (MaxThreads <= 0)
            {
                throw new ArgumentOutOfRangeException("maxThreads");
            }

            if (string.IsNullOrWhiteSpace(QueueName))
            {
                throw new ArgumentNullException("queueName");
            }

            _provider = Constants.DefaultProvider;
            _cts = new CancellationTokenSource();

            _handlers = new List<Action<DispatchContext<TMessage>>>();
            _timeoutHandlers = new List<Action<DispatchContext<TMessage>>>();
            _completeHandlers = new List<Action<DispatchContext<TMessage>>>();
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

            if (handler == null)
            {
                throw new ArgumentNullException("handler");
            }

            _handlers.Add(handler);

            if (_handlers.Count == 1)
            {
                var assistant = new ReceptionAssistant(QueueName, _cts.Token);

                Task.Run(() =>
                {
                    var provider = QueueProviderFactory.CreateProvider(_provider);
                    provider.Dequeue<TMessage>(assistant, Dispatch);

                }, _cts.Token);
            }

            return this;
        }

        private void Dispatch(ReceptionContext<TMessage> receptionContext)
        {
            var dispatch = new DispatchModel();

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
                var task = Task.Run(() =>
                {
                    Thread.Sleep(100); // for ensure handler task complete after dispatch task

                    try
                    {
                        handler(dispatchContext);
                    }
                    catch (Exception ex)
                    {
                        dispatchContext.LogException(ex);
                    }

                }, dispatch.CTS.Token);

                dispatch.Tasks.Add(task);
            }

            Task.Run(() =>
            {
                var timeout = Timeout.HasValue ? (int)Timeout.Value.TotalMilliseconds : Constants.DefaultTimeoutMilliseconds;

                var inTime = Task.WaitAll(dispatch.Tasks.ToArray(), timeout, dispatch.CTS.Token);

                if (!dispatch.CTS.IsCancellationRequested)
                {
                    if (inTime)
                    {
                        ContinueOnSuccess(receptionContext, dispatchContext, dispatch);
                    }
                    else
                    {
                        ContinueOnTimeout(receptionContext, dispatchContext, dispatch);
                    }
                }

            }, dispatch.CTS.Token);
        }

        private void ContinueOnSuccess(ReceptionContext<TMessage> receptionContext, DispatchContext<TMessage> dispatchContext, DispatchModel dispatch)
        {
            if (!dispatch.CTS.IsCancellationRequested)
            {
                lock (dispatch.Locker)
                {
                    if (!dispatch.CTS.IsCancellationRequested)
                    {
                        foreach (var complete in _completeHandlers)
                        {
                            try { complete(dispatchContext); }
                            catch { }
                        }

                        dispatch.Dispose();
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
                        foreach (var timeout in _timeoutHandlers)
                        {
                            try { timeout(dispatchContext); }
                            catch { }
                        }

                        dispatch.Dispose();
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
            if (_cts.IsCancellationRequested)
            {
                throw new InvalidOperationException("Consumer already disposed");
            }
        }

        public void Dispose()
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                _cts.Dispose();
            }

            _handlers.Clear();
            _timeoutHandlers.Clear();
            _completeHandlers.Clear();
        }
    }
}
