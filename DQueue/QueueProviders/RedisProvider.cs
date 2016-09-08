using DQueue.Helpers;
using DQueue.Interfaces;
using StackExchange.Redis;
using System;
using System.Threading;

namespace DQueue.QueueProviders
{
    public class RedisProvider : IQueueProvider
    {
        static Lazy<ConnectionMultiplexer> _redisConnectionFactory = new Lazy<ConnectionMultiplexer>(() =>
        {
            var redisConnectionString = ConfigSource.GetConnection("Redis_Connection");
            var resisConfiguration = ConfigurationOptions.Parse(redisConnectionString);
            return ConnectionMultiplexer.Connect(resisConfiguration);
        }, true);

        private const string SubscriberKey = "$RedisQueueSubscriberKey$";
        private const string SubscriberValue = "$RedisQueueSubscriberValue$";
        private const string HashStorageQueueName = "-$Hash$";

        private readonly ConnectionMultiplexer _connectionFactory;

        public RedisProvider()
            : this(_redisConnectionFactory.Value)
        {
        }

        public RedisProvider(ConnectionMultiplexer connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public bool IgnoreHash { get; set; }

        public bool ExistsMessage(string queueName, object message)
        {
            if (string.IsNullOrWhiteSpace(queueName) || message == null)
            {
                return false;
            }

            var json = message.Serialize();
            var hash = json.GetMD5();

            var database = _connectionFactory.GetDatabase();
            return database.HashExists(queueName + HashStorageQueueName, hash);
        }

        public void Enqueue(string queueName, object message)
        {
            if (string.IsNullOrWhiteSpace(queueName) || message == null)
            {
                return;
            }

            var json = message.Serialize();
            var database = _connectionFactory.GetDatabase();

            string hash = null;
            if (!IgnoreHash)
            {
                hash = json.GetMD5();
                if (database.HashExists(queueName + HashStorageQueueName, hash))
                {
                    return;
                }
            }

            database.ListLeftPush(queueName, json.AddEnqueueTime());

            if (!IgnoreHash)
            {
                database.HashSet(queueName + HashStorageQueueName, hash, 1);
            }

            var subscriber = _connectionFactory.GetSubscriber();
            subscriber.Publish(queueName + SubscriberKey, SubscriberValue);
        }

        public void Dequeue<TMessage>(ReceptionAssistant assistant, Action<ReceptionContext<TMessage>> handler)
        {
            if (assistant == null || string.IsNullOrWhiteSpace(assistant.QueueName) || handler == null)
            {
                return;
            }

            var subscriber = _connectionFactory.GetSubscriber();
            var database = _connectionFactory.GetDatabase();

            var receptionStatus = ReceptionStatus.Listen;

            subscriber.Subscribe(assistant.QueueName + SubscriberKey, (channel, val) =>
            {
                if (val == SubscriberValue)
                {
                    lock (assistant.MonitorLocker)
                    {
                        Monitor.Pulse(assistant.MonitorLocker);
                    }
                }
            });

            assistant.Cancellation.Register(() =>
            {
                subscriber.Unsubscribe(assistant.QueueName + SubscriberKey);

                receptionStatus = ReceptionStatus.Withdraw;

                lock (assistant.MonitorLocker)
                {
                    Monitor.PulseAll(assistant.MonitorLocker);
                }

                var items = database.ListRange(assistant.ProcessingQueueName);
                database.ListRightPush(assistant.QueueName, items);
                database.KeyDelete(assistant.ProcessingQueueName);
            });

            while (true)
            {
                if (receptionStatus == ReceptionStatus.Withdraw)
                {
                    break;
                }

                var message = default(TMessage);
                var item = RedisValue.Null;

                lock (assistant.MonitorLocker)
                {
                    if (database.ListLength(assistant.QueueName) == 0)
                    {
                        receptionStatus = ReceptionStatus.Listen;
                        Monitor.Wait(assistant.MonitorLocker);
                    }

                    try
                    {
                        item = database.ListRightPopLeftPush(assistant.QueueName, assistant.ProcessingQueueName);
                        message = item.GetString().Deserialize<TMessage>();
                    }
                    catch { }
                }

                if (message != null)
                {
                    receptionStatus = ReceptionStatus.Process;

                    handler(new ReceptionContext<TMessage>(message, (sender, status) =>
                    {
                        if (status == ReceptionStatus.Complete)
                        {
                            database.ListRemove(assistant.ProcessingQueueName, item, 1);
                            database.HashDelete(assistant.QueueName + HashStorageQueueName, item.GetString().RemoveEnqueueTime().GetMD5());
                        }
                        else if (status == ReceptionStatus.Retry)
                        {
                            database.ListRemove(assistant.ProcessingQueueName, item, 1);
                            database.ListLeftPush(assistant.QueueName, item.GetString().RemoveEnqueueTime().AddEnqueueTime());
                        }
                    }));
                }
            }
        }

    }
}
