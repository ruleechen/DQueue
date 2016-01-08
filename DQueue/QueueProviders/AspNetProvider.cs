using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            if (!_lockers.ContainsKey(key))
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

        public void Dequeue<TMessage>(string queueName, Action<TMessage, ReceptionContext> handler)
        {
            if (string.IsNullOrWhiteSpace(queueName) || handler == null)
            {
                return;
            }

            var queue = GetQueue(queueName);

            ReceptionStatus = ReceptionStatus.Listen;

            while (true)
            {
                if (ReceptionStatus == ReceptionStatus.BreakOff)
                {
                    break;
                }

                if (queue.Count > 0 &&
                    ReceptionStatus == ReceptionStatus.Listen &&
                    ReceptionStatus != ReceptionStatus.Suspend)
                {
                    lock (GetLocker(queueName))
                    {
                        if (queue.Count > 0 &&
                            ReceptionStatus == ReceptionStatus.Listen &&
                            ReceptionStatus != ReceptionStatus.Suspend)
                        {
                            ReceptionStatus = ReceptionStatus.Process;

                            var context = new ReceptionContext(this);

                            var message = queue.Dequeue();

                            handler((TMessage)message, context);
                        }
                    }
                }

                System.Threading.Thread.Sleep(10);
            }
        }

        public ReceptionStatus ReceptionStatus
        {
            get;
            set;
        }

        public void RequestStop()
        {
            ReceptionStatus = ReceptionStatus.BreakOff;
            System.Threading.Thread.Sleep(20);
        }
    }
}
