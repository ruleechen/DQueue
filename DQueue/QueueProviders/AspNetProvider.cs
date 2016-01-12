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
        static readonly Dictionary<string, object> _lockers;
        static readonly Dictionary<string, Queue<object>> _queues;

        static AspNetProvider()
        {
            _lockers = new Dictionary<string, object>();
            _queues = new Dictionary<string, Queue<object>>();
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

        private static Queue<object> GetQueue(string key)
        {
            if (!_queues.ContainsKey(key))
            {
                lock (GetLocker(key))
                {
                    if (!_queues.ContainsKey(key))
                    {
                        _queues.Add(key, new Queue<object>());
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
                queue.Enqueue(message);
            }
        }

        public void Dequeue<TMessage>(string queueName, Action<TMessage, ReceptionContext> handler, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(queueName) || handler == null)
            {
                return;
            }

            var queue = GetQueue(queueName);

            var receptionStatus = ReceptionStatus.Listen;

            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                if (queue.Count > 0 &&
                    receptionStatus == ReceptionStatus.Listen)
                {
                    object message = null;

                    lock (GetLocker(queueName))
                    {
                        if (queue.Count > 0 &&
                            receptionStatus == ReceptionStatus.Listen)
                        {
                            message = queue.Dequeue();
                        }
                    }

                    if (message != null)
                    {
                        var context = new ReceptionContext((status) => { receptionStatus = status; });
                        receptionStatus = ReceptionStatus.Process;
                        handler((TMessage)message, context);
                    }
                }

                Thread.Sleep(100);
            }
        }
    }
}
