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
        static readonly Dictionary<string, object> _lockers;

        static RedisProvider()
        {
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

            var processingQueueName = queueName + "_processing";

            var database = _connectionFactory.GetDatabase();

            token.Register(() =>
            {
                var items = database.ListRange(processingQueueName);

                database.ListRightPush(queueName, items);

                database.KeyDelete(processingQueueName);
            });

            var receptionStatus = ReceptionStatus.Listen;

            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                if (database.ListLength(queueName) > 0 &&
                    receptionStatus == ReceptionStatus.Listen)
                {
                    var item = RedisValue.Null;

                    lock (GetLocker(queueName))
                    {
                        if (database.ListLength(queueName) > 0 &&
                            receptionStatus == ReceptionStatus.Listen)
                        {
                            item = database.ListRightPopLeftPush(queueName, processingQueueName);
                        }
                    }

                    if (item.HasValue)
                    {
                        var message = JsonConvert.DeserializeObject<TMessage>(item);

                        var context = new ReceptionContext((status) =>
                        {
                            receptionStatus = status;
                            database.ListRemove(processingQueueName, item, 1);
                        });

                        receptionStatus = ReceptionStatus.Process;
                        handler(message, context);
                    }
                }

                Thread.Sleep(100);
            }
        }
    }
}
