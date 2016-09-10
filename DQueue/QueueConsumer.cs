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
        private readonly QueueProvider _provider;
        private readonly CancellationTokenSource _cts;
        private readonly List<ReceptionContext<TMessage>> _pool;
        private readonly ReceptionAssistant _receptionAssistant;

        private readonly List<Action<DispatchContext<TMessage>>> _handlers;
        private readonly List<Action<DispatchContext<TMessage>>> _timeoutHandlers;
        private readonly List<Action<DispatchContext<TMessage>>> _completeHandlers;

        public string QueueName { get; set; }
        public int MaxThreads { get; set; }
        public TimeSpan? Timeout { get; set; }

        public QueueConsumer()
            : this(null, Constants.DefaultMaxParallelThreads)
        {
        }

        public QueueConsumer(string queueName)
            : this(queueName, Constants.DefaultMaxParallelThreads)
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

            if (MaxThreads < 1)
            {
                throw new ArgumentOutOfRangeException("maxThreads");
            }

            if (string.IsNullOrWhiteSpace(QueueName))
            {
                throw new ArgumentNullException("queueName");
            }

            _provider = Constants.DefaultProvider;
            _cts = new CancellationTokenSource();
            _pool = new List<ReceptionContext<TMessage>>();
            _receptionAssistant = new ReceptionAssistant(QueueName, _cts.Token);

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
                Task.Run(() =>
                {
                    var provider = QueueProviderFactory.CreateProvider(_provider);
                    provider.Dequeue<TMessage>(_receptionAssistant, Pooling);

                }, _cts.Token);
            }

            return this;
        }

        private bool _dispatchingPool;
        private CancellationTokenSource _delayCancellation;

        private void Pooling(ReceptionContext<TMessage> receptionContext)
        {
            if (_delayCancellation != null)
            {
                _delayCancellation.Cancel();
                _delayCancellation = null;
            }

            if (_dispatchingPool)
            {
                lock (_receptionAssistant.PoolingLocker)
                {
                    Monitor.Wait(_receptionAssistant.PoolingLocker);
                }
            }

            _pool.Add(receptionContext);

            if (_pool.Count >= MaxThreads)
            {
                DispatchPool();
            }
            else
            {
                _delayCancellation = new CancellationTokenSource();
                Task.Delay(1000, _delayCancellation.Token).ContinueWith(DispatchPool);
            }
        }

        private void DispatchPool(Task delay = null)
        {
            if (delay != null && delay.IsCanceled)
            {
                return;
            }

            _dispatchingPool = true;

            Task.Run(() =>
            {
                try
                {
                    Parallel.ForEach(_pool, new ParallelOptions
                    {
                        CancellationToken = _cts.Token

                    }, DispatchMessage);
                }
                catch (OperationCanceledException)
                {
                }

                _pool.Clear();

                _dispatchingPool = false;

                lock (_receptionAssistant.PoolingLocker)
                {
                    Monitor.Pulse(_receptionAssistant.PoolingLocker);
                }

            }, _cts.Token);
        }

        private void DispatchMessage(ReceptionContext<TMessage> receptionContext)
        {
            var dispatchContext = new DispatchContext<TMessage>(
                receptionContext.Message, (sender, status) =>
            {
                if (status == DispatchStatus.Complete)
                {
                    ContinueOnSuccess(receptionContext, sender);
                }
                else if (status == DispatchStatus.Timeout)
                {
                    ContinueOnTimeout(receptionContext, sender);
                }
            });

            var timeout = Timeout.HasValue ? Timeout.Value : TimeSpan.FromMilliseconds(Constants.DefaultTimeoutMilliseconds);
            var timeoutCancellation = new CancellationTokenSource(timeout);
            var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCancellation.Token);

            try
            {
                var options = new ParallelOptions
                {
                    CancellationToken = linkedCancellation.Token
                };

                var result = Parallel.ForEach(_handlers, options, (handler) =>
                {
                    try
                    {
                        handler(dispatchContext);
                    }
                    catch (Exception ex)
                    {
                        dispatchContext.LogException(ex);
                    }
                });

                if (result.IsCompleted)
                {
                    ContinueOnSuccess(receptionContext, dispatchContext);
                }
            }
            catch (OperationCanceledException)
            {
                if (!_cts.Token.IsCancellationRequested)
                {
                    ContinueOnTimeout(receptionContext, dispatchContext);
                }
            }
        }

        private void ContinueOnSuccess(ReceptionContext<TMessage> receptionContext, DispatchContext<TMessage> dispatchContext)
        {
            if (!dispatchContext.Cancellation.IsCancellationRequested)
            {
                lock (dispatchContext.Locker)
                {
                    if (!dispatchContext.Cancellation.IsCancellationRequested)
                    {
                        foreach (var complete in _completeHandlers)
                        {
                            try { complete(dispatchContext); }
                            catch { }
                        }

                        dispatchContext.Dispose();
                        receptionContext.Success();
                    }
                }
            }
        }

        private void ContinueOnTimeout(ReceptionContext<TMessage> receptionContext, DispatchContext<TMessage> dispatchContext)
        {
            if (!dispatchContext.Cancellation.IsCancellationRequested)
            {
                lock (dispatchContext.Locker)
                {
                    if (!dispatchContext.Cancellation.IsCancellationRequested)
                    {
                        foreach (var timeout in _timeoutHandlers)
                        {
                            try { timeout(dispatchContext); }
                            catch { }
                        }

                        dispatchContext.Dispose();
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

            _pool.Clear();
            _handlers.Clear();
            _timeoutHandlers.Clear();
            _completeHandlers.Clear();
        }
    }
}
