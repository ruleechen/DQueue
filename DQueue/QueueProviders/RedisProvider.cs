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
            var subscriber = _connectionFactory.GetSubscriber();
            subscriber.Publish(queueName, json);
        }

        public void Dequeue<TMessage>(string queueName, Action<TMessage, ReceptionContext> handler, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(queueName) || handler == null)
            {
                return;
            }

            var receptionStatus = ReceptionStatus.Listen;

            while (true)
            {
                token.ThrowIfCancellationRequested();

                if (receptionStatus == ReceptionStatus.BreakOff)
                {
                    break;
                }

                //var subscriber = _connectionFactory.GetSubscriber();

                //subscriber.Unsubscribe(

                //subscriber.Subscribe(queueName, (channel, body) =>
                //{

                //});
            }
        }
    }
}
