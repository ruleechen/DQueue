using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
            var monitorLocker = ReceptionAssistant.GetLocker(queueName, ReceptionAssistant.Flag_MonitorLocker);

            lock (monitorLocker)
            {
                queue.Add(message);
                Monitor.Pulse(monitorLocker);
            }
        }

        public void Dequeue<TMessage>(ReceptionAssistant assistant, Action<ReceptionContext<TMessage>> handler)
        {
            if (assistant == null || string.IsNullOrWhiteSpace(assistant.QueueName) || handler == null)
            {
                return;
            }

            var queue = GetQueue(assistant.QueueName);
            var queueProcessing = GetQueue(assistant.ProcessingQueueName);

            var receptionLocker = new object();
            var receptionStatus = ReceptionStatus.Listen;

            assistant.RegisterFallback(() =>
            {
                foreach (var item in queueProcessing)
                {
                    queue.Insert(0, item);
                }

                queueProcessing.Clear();
            });

            assistant.RegisterCancel(1, false, () =>
            {
                lock (receptionLocker)
                {
                    receptionStatus = ReceptionStatus.Withdraw;
                }
            });

            assistant.RegisterCancel(2, true, () =>
            {
                lock (assistant.MonitorLocker)
                {
                    Monitor.PulseAll(assistant.MonitorLocker);
                }
            });

            while (true)
            {
                lock (receptionLocker)
                {
                    if (receptionStatus == ReceptionStatus.Process)
                    {
                        Monitor.Wait(receptionLocker);
                    }
                }

                object message = null;

                lock (assistant.MonitorLocker)
                {
                    if (queue.Count == 0)
                    {
                        Monitor.Wait(assistant.MonitorLocker);
                    }

                    if (receptionStatus == ReceptionStatus.Listen)
                    {
                        message = queue[0];
                        queue.RemoveAt(0);
                        queueProcessing.Add(message);
                    }
                }

                if (receptionStatus == ReceptionStatus.Withdraw)
                {
                    break;
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

                        lock (receptionLocker)
                        {
                            Monitor.Pulse(receptionLocker);
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
