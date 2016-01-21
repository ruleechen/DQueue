using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using DQueue.Helpers;
using DQueue.Interfaces;

namespace DQueue.QueueProviders
{
    public class AspNetProvider : IQueueProvider
    {
        #region static
        static readonly HashSet<string> _initialized;
        static readonly Dictionary<string, List<object>> _queues;

        static AspNetProvider()
        {
            _initialized = new HashSet<string>();
            _queues = new Dictionary<string, List<object>>();
        }

        private static List<object> GetQueue(string key)
        {
            if (!_queues.ContainsKey(key))
            {
                lock (typeof(AspNetProvider))
                {
                    if (!_queues.ContainsKey(key))
                    {
                        _queues.Add(key, new List<object>());
                    }
                }
            }

            return _queues[key];
        }
        #endregion

        public void Enqueue(string queueName, object message)
        {
            if (string.IsNullOrWhiteSpace(queueName) || message == null)
            {
                return;
            }

            var queue = GetQueue(queueName);

            lock (queue)
            {
                queue.Add(message);
                Monitor.Pulse(queue);
            }
        }

        public void Dequeue<TMessage>(string queueName, Action<ReceptionContext<TMessage>> handler, CancellationPack token)
        {
            if (string.IsNullOrWhiteSpace(queueName) || handler == null)
            {
                return;
            }

            var processingQueueName = QueueNameGenerator.GetProcessingQueueName(queueName);

            var queue = GetQueue(queueName);

            var queueProcessing = GetQueue(processingQueueName);

            if (!_initialized.Contains(queueName))
            {
                lock (queue)
                {
                    if (!_initialized.Contains(queueName))
                    {
                        foreach (var item in queueProcessing)
                        {
                            queue.Insert(0, item);
                        }

                        queueProcessing.Clear();

                        _initialized.Add(queueName);
                    }
                }
            }

            var receptionLocker = new object();
            var receptionStatus = ReceptionStatus.Listen;

            token.Register(1, false, () =>
            {
                lock (receptionLocker)
                {
                    receptionStatus = ReceptionStatus.Withdraw;
                }
            });

            token.Register(2, true, () =>
            {
                lock (queue)
                {
                    Monitor.PulseAll(queue);
                }
            });

            token.Register(3, true, () =>
            {
                foreach (var item in queueProcessing)
                {
                    queue.Insert(0, item);
                }

                queueProcessing.Clear();

                _initialized.Remove(queueName);
            });

            while (true)
            {
                lock (queue)
                {
                    if (queue.Count == 0)
                    {
                        Monitor.Wait(queue);
                    }
                }

                if (receptionStatus == ReceptionStatus.Withdraw)
                {
                    break;
                }

                object message = null;

                if (receptionStatus == ReceptionStatus.Listen)
                {
                    if (queue.Count > 0)
                    {
                        message = queue[0];
                        queue.RemoveAt(0);
                        queueProcessing.Add(message);
                    }
                }

                if (message != null)
                {
                    var context = new ReceptionContext<TMessage>((TMessage)message, (sender, status) =>
                    {
                        if (status == ReceptionStatus.Success)
                        {
                            queueProcessing.Remove(message);
                            status = ReceptionStatus.Listen;
                        }

                        if (receptionStatus != ReceptionStatus.Withdraw)
                        {
                            lock (receptionLocker)
                            {
                                if (receptionStatus != ReceptionStatus.Withdraw)
                                {
                                    receptionStatus = status;
                                }
                            }
                        }
                    });

                    if (receptionStatus != ReceptionStatus.Withdraw)
                    {
                        lock (receptionLocker)
                        {
                            if (receptionStatus != ReceptionStatus.Withdraw)
                            {
                                receptionStatus = ReceptionStatus.Process;
                                handler(context);
                            }
                        }
                    }
                }

                //Thread.Sleep(100);
            }
        }
    }
}
