using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using DQueue.Interfaces;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace DQueue.QueueProviders
{
    public class RedisProvider : IQueueProvider
    {
        #region static
        static readonly HashSet<string> _initialized;
        static readonly Dictionary<string, object> _lockers;

        static RedisProvider()
        {
            _initialized = new HashSet<string>();
            _lockers = new Dictionary<string, object>();
        }

        private static object GetLocker(string key)
        {
            if (!_lockers.ContainsKey(key))
            {
                lock (typeof(RedisProvider))
                {
                    if (!_lockers.ContainsKey(key))
                    {
                        _lockers.Add(key, new object());
                    }
                }
            }

            return _lockers[key];
        }
        #endregion

        private readonly ConnectionMultiplexer _connectionFactory;

        public RedisProvider(ConnectionMultiplexer connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public void Enqueue(string queueName, object message)
        {
            if (string.IsNullOrWhiteSpace(queueName) || message == null)
            {
                return;
            }

            var json = JsonConvert.SerializeObject(message);

            var database = _connectionFactory.GetDatabase();

            database.ListLeftPush(queueName, json);
        }

        public void Dequeue<TMessage>(string queueName, Action<TMessage, ReceptionContext> handler, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(queueName) || handler == null)
            {
                return;
            }

            var processingQueueName = QueueHelpers.GetProcessingQueueName(queueName);

            var database = _connectionFactory.GetDatabase();

            if (!_initialized.Contains(queueName))
            {
                lock (GetLocker(queueName))
                {
                    if (!_initialized.Contains(queueName))
                    {
                        Action fallback = null;

                        token.Register(fallback = () =>
                        {
                            var items = database.ListRange(processingQueueName);

                            database.ListRightPush(queueName, items);

                            database.KeyDelete(processingQueueName);

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

                if (receptionStatus == ReceptionStatus.Listen && database.ListLength(queueName) > 0)
                {
                    var item = RedisValue.Null;

                    lock (GetLocker(queueName))
                    {
                        if (receptionStatus == ReceptionStatus.Listen && database.ListLength(queueName) > 0)
                        {
                            item = database.ListRightPopLeftPush(queueName, processingQueueName);
                        }
                    }

                    if (item.HasValue)
                    {
                        var message = JsonConvert.DeserializeObject<TMessage>(item);

                        var context = new ReceptionContext((sender, status) =>
                        {
                            if (status == ReceptionStatus.Success)
                            {
                                database.ListRemove(processingQueueName, item, 1);
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

                        handler(message, context);
                    }
                }

                Thread.Sleep(100);
            }
        }
    }
}
