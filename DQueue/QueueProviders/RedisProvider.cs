using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using DQueue.Helpers;
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

        public void Dequeue<TMessage>(string queueName, Action<ReceptionContext<TMessage>> handler, CancellationPack token)
        {
            if (string.IsNullOrWhiteSpace(queueName) || handler == null)
            {
                return;
            }

            var processingQueueName = QueueNameGenerator.GetProcessingQueueName(queueName);

            var database = _connectionFactory.GetDatabase();

            if (!_initialized.Contains(queueName))
            {
                lock (GetLocker(queueName))
                {
                    if (!_initialized.Contains(queueName))
                    {
                        var items = database.ListRange(processingQueueName);

                        database.ListRightPush(queueName, items);

                        database.KeyDelete(processingQueueName);

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

            });

            token.Register(3, true, () =>
            {
                var items = database.ListRange(processingQueueName);

                database.ListRightPush(queueName, items);

                database.KeyDelete(processingQueueName);

                _initialized.Add(queueName);
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

                        var context = new ReceptionContext<TMessage>(message, (sender, status) =>
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
                }

                Thread.Sleep(100);
            }
        }
    }
}
