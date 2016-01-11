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

            database.ListLeftPush(queueName, json, When.Always, CommandFlags.None);
        }

        public void Dequeue<TMessage>(string queueName, Action<TMessage, ReceptionContext> handler, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(queueName) || handler == null)
            {
                return;
            }

            var processingQueueName = queueName + "_processing";

            var database = _connectionFactory.GetDatabase();

            var receptionStatus = ReceptionStatus.Listen;

            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                if (receptionStatus == ReceptionStatus.Listen)
                {
                    var json = database.ListRightPop(queueName, CommandFlags.None);
                    if (json.HasValue)
                    {
                        var message = JsonConvert.DeserializeObject<TMessage>(json);

                        var context = new ReceptionContext((status) =>
                        {
                            receptionStatus = status;
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
