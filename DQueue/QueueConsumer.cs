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

        private readonly List<Action<DispatchContext<TMessage>>> _messageHandlers;
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

            _messageHandlers = new List<Action<DispatchContext<TMessage>>>();
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

            _messageHandlers.Add(handler);

            if (_messageHandlers.Count == 1)
            {
                Task.Run(() =>
                {
                    var assistant = new ReceptionAssistant<TMessage>(QueueName, _cts.Token);
                    var provider = QueueProviderFactory.CreateProvider(_provider);
                    provider.Dequeue<TMessage>(assistant, Pooling);

                }, _cts.Token);
            }

            return this;
        }

        private void Pooling(ReceptionContext<TMessage> receptionContext)
        {
            var assistant = receptionContext.Assistant;

            if (assistant.DelayCancellation != null)
            {
                assistant.DelayCancellation.Cancel();
                assistant.DelayCancellation = null;
            }

            if (assistant.IsDispatchingPool)
            {
                lock (assistant.PoolingLocker)
                {
                    Monitor.Wait(assistant.PoolingLocker);
                }
            }

            assistant.Pool.Add(receptionContext);

            if (assistant.Pool.Count >= MaxThreads)
            {
                DispatchPool(assistant);
            }
            else
            {
                assistant.DelayCancellation = new CancellationTokenSource();

                Task.Delay(1000, assistant.DelayCancellation.Token).ContinueWith((task) =>
                {
                    if (!task.IsCanceled)
                    {
                        DispatchPool(assistant);
                    }
                });
            }
        }

        private void DispatchPool(ReceptionAssistant<TMessage> assistant)
        {
            assistant.IsDispatchingPool = true;

            Task.Run(() =>
            {
                try
                {
                    Parallel.ForEach(assistant.Pool, new ParallelOptions
                    {
                        CancellationToken = _cts.Token

                    }, DispatchMessage);
                }
                catch (OperationCanceledException)
                {
                }

                assistant.Pool.Clear();

                assistant.IsDispatchingPool = false;

                lock (assistant.PoolingLocker)
                {
                    Monitor.Pulse(assistant.PoolingLocker);
                }

            }, _cts.Token);
        }

        private void DispatchMessage(ReceptionContext<TMessage> receptionContext)
        {
            var timeout = Timeout.HasValue ? Timeout.Value : TimeSpan.FromMilliseconds(Constants.DefaultTimeoutMilliseconds);

            var dispatchContext = new DispatchContext<TMessage>(
                receptionContext.Message, timeout, _cts, (sender, status) =>
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

            try
            {
                var options = new ParallelOptions
                {
                    CancellationToken = dispatchContext.LinkedCancellation.Token
                };

                var result = Parallel.ForEach(_messageHandlers, options, (handler) =>
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
            if (!dispatchContext.OwnedCancellation.IsCancellationRequested)
            {
                lock (dispatchContext.Locker)
                {
                    if (!dispatchContext.OwnedCancellation.IsCancellationRequested)
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
            if (!dispatchContext.OwnedCancellation.IsCancellationRequested)
            {
                lock (dispatchContext.Locker)
                {
                    if (!dispatchContext.OwnedCancellation.IsCancellationRequested)
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

            _messageHandlers.Clear();
            _timeoutHandlers.Clear();
            _completeHandlers.Clear();
        }
    }
}
