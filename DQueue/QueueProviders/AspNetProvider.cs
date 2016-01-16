﻿using System;
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
        static readonly HashSet<string> _initialized;
        static readonly Dictionary<string, object> _lockers;
        static readonly Dictionary<string, List<object>> _queues;

        static AspNetProvider()
        {
            _initialized = new HashSet<string>();
            _lockers = new Dictionary<string, object>();
            _queues = new Dictionary<string, List<object>>();
        }

        private static object GetLocker(string key)
        {
            if (!_lockers.ContainsKey(key))
            {
                lock (typeof(AspNetProvider))
                {
                    if (!_lockers.ContainsKey(key))
                    {
                        _lockers.Add(key, new object());
                    }
                }
            }

            return _lockers[key];
        }

        private static List<object> GetQueue(string key)
        {
            if (!_queues.ContainsKey(key))
            {
                lock (GetLocker(key))
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

            lock (GetLocker(queueName))
            {
                queue.Add(message);
            }
        }

        public void Dequeue<TMessage>(string queueName, Action<ReceptionContext<TMessage>> handler, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(queueName) || handler == null)
            {
                return;
            }

            var processingQueueName = QueueHelpers.GetProcessingQueueName(queueName);

            var queue = GetQueue(queueName);

            var queueProcessing = GetQueue(processingQueueName);

            if (!_initialized.Contains(queueName))
            {
                lock (GetLocker(queueName))
                {
                    if (!_initialized.Contains(queueName))
                    {
                        Action fallback = null;

                        token.Register(fallback = () =>
                        {
                            foreach (var item in queueProcessing)
                            {
                                queue.Insert(0, item);
                            }

                            queueProcessing.Clear();

                            _initialized.Remove(queueName);
                        });

                        fallback();

                        _initialized.Add(queueName);
                    }
                }
            }

            var receptionLocker = new object();
            var receptionStatus = ReceptionStatus.Listen;

            token.Register(() =>
            {
                lock (receptionLocker)
                {
                    receptionStatus = ReceptionStatus.Withdraw;
                }
            });

            while (true)
            {
                if (receptionStatus == ReceptionStatus.Withdraw)
                {
                    break;
                }

                if (receptionStatus == ReceptionStatus.Listen && queue.Count > 0)
                {
                    object message = null;

                    lock (GetLocker(queueName))
                    {
                        if (receptionStatus == ReceptionStatus.Listen && queue.Count > 0)
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
                                    receptionStatus = status;
                                }
                            }
                        });

                        lock (receptionLocker)
                        {
                            receptionStatus = ReceptionStatus.Process;
                        }

                        handler(context);
                    }
                }

                Thread.Sleep(100);
            }
        }
    }
}
