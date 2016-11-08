using DQueue.Helpers;
using DQueue.Infrastructure;
using DQueue.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DQueue
{
    public class QueueConsumer<TMessage> : IQueueConsumer, IDisposable
        where TMessage : new()
    {
        private readonly bool DISPATCH_MANY = false;

        private readonly QueueProvider _provider;
        private readonly CancellationTokenSource _cts;
        private ReceptionAssistant<TMessage> _assistant;

        private readonly List<Action<DispatchContext<TMessage>>> _messageHandlers;
        private readonly List<Action<DispatchContext<TMessage>>> _timeoutHandlers;
        private readonly List<Action<DispatchContext<TMessage>>> _completeHandlers;
        public event EventHandler<DispatchEventArgs<TMessage>> DispatchStatusChange;

        public string HostId { get; private set; }
        public string QueueName { get; private set; }
        public int MaximumThreads { get; set; }
        public TimeSpan? Timeout { get; set; }
        private Task DequeueTask { get; set; }

        public QueueConsumer()
            : this(null, Constants.DefaultMaxParallelThreads)
        {
        }

        public QueueConsumer(string queueName)
            : this(queueName, Constants.DefaultMaxParallelThreads)
        {
        }

        public QueueConsumer(int maximumThreads)
            : this(null, maximumThreads)
        {
        }

        public QueueConsumer(string queueName, int maximumThreads)
        {
            MaximumThreads = maximumThreads;
            HostId = ConfigSource.GetAppSetting("DQueue.HostId");
            QueueName = queueName ?? QueueNameGenerator.GetQueueName<TMessage>();
            Timeout = ConfigSource.FirstAppSetting("DQueue.ConsumerTimeout", "ConsumerTimeout").AsNullableTimeSpan();

            if (string.IsNullOrWhiteSpace(HostId))
            {
                throw new ArgumentNullException("HostId");
            }

            if (string.IsNullOrWhiteSpace(QueueName))
            {
                throw new ArgumentNullException("queueName");
            }

            if (MaximumThreads < 1)
            {
                throw new ArgumentOutOfRangeException("maximumThreads");
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
            return Receive(1, handler);
        }

        public QueueConsumer<TMessage> Receive(int repeat, Action<DispatchContext<TMessage>> handler)
        {
            CheckDisposed();

            if (repeat <= 0)
            {
                throw new ArgumentOutOfRangeException("repeat");
            }

            if (handler == null)
            {
                throw new ArgumentNullException("handler");
            }

            var count = _messageHandlers.Count;

            for (var i = 0; i < repeat; i++)
            {
                _messageHandlers.Add(handler);
            }

            if (count == 0)
            {
                StartDequeue();
            }

            return this;
        }

        private void StartDequeue()
        {
            DequeueTask = Task.Run(() =>
            {
                _assistant = new ReceptionAssistant<TMessage>(HostId, QueueName);

                try
                {
                    var provider = QueueProviderFactory.CreateProvider(_provider);
                    provider.Dequeue<TMessage>(_assistant, Pooling);
                }
                catch (Exception ex)
                {
                    LogFactory.GetLogger().Error(string.Format("Receive Task Error for queue \"{0}\".", _assistant.QueueName), ex);
                    _assistant.Dispose();
                }

            }, _cts.Token);

            ConsumerHealth.Register(this);
        }

        private void Pooling(ReceptionContext<TMessage> receptionContext)
        {
            var assistant = receptionContext.Assistant;

            if (assistant.IsStopPooling)
            {
                lock (assistant.PoolingLocker)
                {
                    Monitor.Wait(assistant.PoolingLocker);
                }
            }

            if (DISPATCH_MANY)
            {
                if (assistant.DelayCancellation != null)
                {
                    assistant.DelayCancellation.Cancel();
                    assistant.DelayCancellation = null;
                }

                assistant.Pool.Add(receptionContext);

                if (assistant.Pool.Count >= MaximumThreads)
                {
                    DispatchMany(assistant);
                }
                else
                {
                    assistant.DelayCancellation = new CancellationTokenSource();

                    Task.Delay(1000, assistant.DelayCancellation.Token).ContinueWith((task) =>
                    {
                        if (!task.IsCanceled)
                        {
                            DispatchMany(assistant);
                        }
                    });
                }
            }
            else
            {
                lock (assistant.PoolingLocker)
                {
                    assistant.Pool.Add(receptionContext);

                    assistant.IsStopPooling = assistant.Pool.Count >= MaximumThreads;
                }

                DispatchOne(receptionContext);
            }

            if (assistant.IsStopPooling)
            {
                lock (assistant.PoolingLocker)
                {
                    Monitor.Wait(assistant.PoolingLocker);
                }
            }
        }

        private void DispatchOne(ReceptionContext<TMessage> receptionContext)
        {
            var assistant = receptionContext.Assistant;

            receptionContext.OnDone = () =>
            {
                lock (assistant.PoolingLocker)
                {
                    assistant.Pool.Remove(receptionContext);

                    assistant.IsStopPooling = assistant.Pool.Count >= MaximumThreads;

                    if (!assistant.IsStopPooling)
                    {
                        Monitor.Pulse(assistant.PoolingLocker);
                    }
                }
            };

            Task.Run(() =>
            {
                DispatchMessage(receptionContext);

            }, _cts.Token);
        }

        private void DispatchMany(ReceptionAssistant<TMessage> assistant)
        {
            assistant.IsStopPooling = true;

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

                assistant.IsStopPooling = false;

                lock (assistant.PoolingLocker)
                {
                    Monitor.Pulse(assistant.PoolingLocker);
                }

            }, _cts.Token);
        }

        private void DispatchMessage(ReceptionContext<TMessage> receptionContext)
        {
            var timeout = Timeout.HasValue ? Timeout.Value : Constants.DefaultTimeoutTimeSpan.AsNullableTimeSpan().Value;

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

            EmitDispatchStatusChange(dispatchContext);

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
                    dispatchContext.GotoComplete();
                }
            }
            catch (OperationCanceledException)
            {
                if (!_cts.Token.IsCancellationRequested)
                {
                    dispatchContext.GotoTimeout();
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
                        EmitDispatchStatusChange(dispatchContext);

                        foreach (var complete in _completeHandlers)
                        {
                            try { complete(dispatchContext); }
                            catch { }
                        }

                        dispatchContext.Dispose();
                        receptionContext.FeedbackSuccess();
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
                        EmitDispatchStatusChange(dispatchContext);

                        foreach (var timeout in _timeoutHandlers)
                        {
                            try { timeout(dispatchContext); }
                            catch { }
                        }

                        dispatchContext.Dispose();
                        receptionContext.FeedbackTimeout();
                    }
                }
            }
        }

        private void EmitDispatchStatusChange(DispatchContext<TMessage> dispatchContext)
        {
            if (DispatchStatusChange != null)
            {
                var args = new DispatchEventArgs<TMessage>(dispatchContext);
                try { DispatchStatusChange.Invoke(this, args); } catch { }
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

            if (_assistant != null)
            {
                _assistant.Dispose();
            }

            _messageHandlers.Clear();
            _timeoutHandlers.Clear();
            _completeHandlers.Clear();
        }

        public bool IsAlive()
        {
            CheckDisposed();

            return DequeueTask != null &&
                !DequeueTask.IsCompleted &&
                !DequeueTask.IsFaulted;
        }

        public void Rescue()
        {
            CheckDisposed();

            StartDequeue();
        }
    }
}
