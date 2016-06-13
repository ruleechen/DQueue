using DQueue.Helpers;
using DQueue.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DQueue.QueueProviders
{
    public class AspNetProvider : IQueueProvider
    {
        #region static
        static readonly Dictionary<string, List<string>> _queues;
        static readonly Dictionary<string, HashSet<string>> _hashs;

        static AspNetProvider()
        {
            _queues = new Dictionary<string, List<string>>();
            _hashs = new Dictionary<string, HashSet<string>>();
        }

        private static List<string> GetQueue(string key)
        {
            if (!_queues.ContainsKey(key))
            {
                lock (typeof(AspNetProvider))
                {
                    if (!_queues.ContainsKey(key))
                    {
                        _queues.Add(key, new List<string>());
                    }
                }
            }

            return _queues[key];
        }

        private static HashSet<string> GetHashSet(string key)
        {
            if (!_hashs.ContainsKey(key))
            {
                lock (typeof(AspNetProvider))
                {
                    if (!_hashs.ContainsKey(key))
                    {
                        _hashs.Add(key, new HashSet<string>());
                    }
                }
            }

            return _hashs[key];
        }
        #endregion

        public bool IgnoreHash { get; set; }

        public bool ExistsMessage(string queueName, object message)
        {
            if (string.IsNullOrWhiteSpace(queueName) || message == null)
            {
                return false;
            }

            var json = JsonConvert.SerializeObject(message);
            var hash = HashCodeGenerator.Calc(json);

            var hashSet = GetHashSet(queueName);
            return hashSet.Contains(hash);
        }

        public void Enqueue(string queueName, object message)
        {
            if (string.IsNullOrWhiteSpace(queueName) || message == null)
            {
                return;
            }

            var json = JsonConvert.SerializeObject(message);
            var hash = HashCodeGenerator.Calc(json);

            var hashSet = GetHashSet(queueName);
            if (!IgnoreHash && hashSet.Contains(hash))
            {
                return;
            }

            var queue = GetQueue(queueName);
            var monitorLocker = ReceptionAssistant.GetLocker(queueName, ReceptionAssistant.Flag_MonitorLocker);

            lock (monitorLocker)
            {
                queue.Add(json);
                hashSet.Add(hash);
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
            var hashSet = GetHashSet(assistant.QueueName);
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
                lock (receptionLocker)
                {
                    Monitor.PulseAll(receptionLocker);
                }

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

                if (receptionStatus == ReceptionStatus.Withdraw)
                {
                    break;
                }

                object message = null;
                string item = null;

                lock (assistant.MonitorLocker)
                {
                    if (queue.Count == 0)
                    {
                        Monitor.Wait(assistant.MonitorLocker);
                    }

                    if (receptionStatus == ReceptionStatus.Listen)
                    {
                        item = queue[0];
                        queue.RemoveAt(0);
                        queueProcessing.Add(item);
                        if (!string.IsNullOrEmpty(item)) { message = JsonConvert.DeserializeObject<TMessage>(item); }
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
                        if (status == ReceptionStatus.Complete)
                        {
                            queueProcessing.Remove(item);
                            hashSet.Remove(HashCodeGenerator.Calc(item));
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
