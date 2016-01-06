using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DQueue.Interfaces;

namespace DQueue.QueueProviders
{
    public class AspNetProvider : IQueueProvider
    {
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

        public void Send(string queueName, object message)
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

        public void Receive<TMessage>(string queueName, Action<TMessage, ReceptionContext> handler)
        {
            if (string.IsNullOrWhiteSpace(queueName) || handler == null)
            {
                return;
            }

            var queue = GetQueue(queueName);

            var receptionStatus = ReceptionStatus.Querying;

            while (true)
            {
                if (receptionStatus == ReceptionStatus.BreakOff)
                {
                    break;
                }

                if (queue.Count > 0 && receptionStatus == ReceptionStatus.Querying)
                {
                    lock (GetLocker(queueName))
                    {
                        if (queue.Count > 0 && receptionStatus == ReceptionStatus.Querying)
                        {
                            receptionStatus = ReceptionStatus.Processing;

                            var context = new ReceptionContext((status) =>
                            {
                                receptionStatus = status;
                            });

                            var message = queue.Dequeue();

                            handler((TMessage)message, context);
                        }
                    }
                }

                System.Threading.Thread.Sleep(10);
            }
        }
    }
}
