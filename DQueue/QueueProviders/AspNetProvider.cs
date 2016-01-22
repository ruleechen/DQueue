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
        static readonly Dictionary<string, List<object>> _queues;

        static AspNetProvider()
        {
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
            var queueLocker = ReceptionManager.GetQueueLocker(queueName);

            lock (queueLocker)
            {
                queue.Add(message);
                Monitor.Pulse(queueLocker);
            }
        }

        public void Dequeue<TMessage>(ReceptionManager manager, Action<ReceptionContext<TMessage>> handler)
        {
            if (manager == null || string.IsNullOrWhiteSpace(manager.QueueName) || handler == null)
            {
                return;
            }

            var queue = GetQueue(manager.QueueName);
            var queueLocker = manager.QueueLocker();
            var queueProcessing = GetQueue(manager.ProcessingQueueName);

            var receptionLocker = new object();
            var receptionStatus = ReceptionStatus.Listen;

            manager.OnFallback(() =>
            {
                foreach (var item in queueProcessing)
                {
                    queue.Insert(0, item);
                }

                queueProcessing.Clear();
            });

            manager.OnCancel(1, false, () =>
            {
                lock (receptionLocker)
                {
                    receptionStatus = ReceptionStatus.Withdraw;
                }
            });

            manager.OnCancel(2, true, () =>
            {
                lock (queueLocker)
                {
                    Monitor.PulseAll(queueLocker);
                }
            });

            while (true)
            {
                lock (queueLocker)
                {
                    if (queue.Count == 0)
                    {
                        Monitor.Wait(queueLocker);
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
