using DQueue.Interfaces;
using StackExchange.Redis;
using System;
using System.Threading;

namespace DQueue.QueueProviders
{
    public class RedisProvider : IQueueProvider
    {
        class ConnectionFactoryWrapper
        {
            private IDatabase _database;
            private ISubscriber _subscriber;

            public ConnectionFactoryWrapper(IConnectionMultiplexer connection)
            {
                _database = connection.GetDatabase();
                _subscriber = connection.GetSubscriber();
            }

            public IDatabase GetDatabase()
            {
                return _database;
            }

            public ISubscriber GetSubscriber()
            {
                return _subscriber;
            }
        }

        static Lazy<ConnectionFactoryWrapper> _redisConnectionFactory = new Lazy<ConnectionFactoryWrapper>(() =>
        {
            var redisConnectionString = ConfigSource.GetConnection("Redis_Connection");
            var resisConfiguration = ConfigurationOptions.Parse(redisConnectionString);
            var connection = ConnectionMultiplexer.Connect(resisConfiguration);
            return new ConnectionFactoryWrapper(connection);
        }, true);

        private const string SubscriberKey = "$RedisQueueSubscriberKey$";
        private const string SubscriberValue = "$RedisQueueSubscriberValue$";
        private const string HashStorageQueueName = "-$Hash$";

        private ConnectionFactoryWrapper _connectionFactory;

        public RedisProvider()
        {
            _connectionFactory = _redisConnectionFactory.Value;
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

        public void Dequeue<TMessage>(ReceptionAssistant<TMessage> assistant, Action<ReceptionContext<TMessage>> handler)
        {
            if (assistant == null || string.IsNullOrWhiteSpace(assistant.QueueName) || handler == null)
            {
                return;
            }

            var subscriber = _connectionFactory.GetSubscriber();
            var database = _connectionFactory.GetDatabase();

            var receptionStatus = ReceptionStatus.None;

            subscriber.Subscribe(assistant.QueueName + SubscriberKey, (channel, val) =>
            {
                if (val == SubscriberValue)
                {
                    lock (assistant.DequeueLocker)
                    {
                        Monitor.Pulse(assistant.DequeueLocker);
                    }
                }
            });

            RequeueProcessingMessages(assistant, database);

            assistant.Cancellation.Register(() =>
            {
                subscriber.Unsubscribe(assistant.QueueName + SubscriberKey);

                receptionStatus = ReceptionStatus.Withdraw;

                lock (assistant.DequeueLocker)
                {
                    Monitor.PulseAll(assistant.DequeueLocker);
                }

                RequeueProcessingMessages(assistant, database);
            });

            while (true)
            {
                if (receptionStatus == ReceptionStatus.Withdraw)
                {
                    break;
                }

                var message = default(TMessage);
                var item = RedisValue.Null;

                lock (assistant.DequeueLocker)
                {
                    if (database.ListLength(assistant.QueueName) == 0)
                    {
                        Monitor.Wait(assistant.DequeueLocker);
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
                    handler(new ReceptionContext<TMessage>(message, assistant, (sender, status) =>
                    {
                        if (status == ReceptionStatus.Completed)
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

        private static void RequeueProcessingMessages<TMessage>(ReceptionAssistant<TMessage> assistant, IDatabase database)
        {
            var items = database.ListRange(assistant.ProcessingQueueName);
            database.ListRightPush(assistant.QueueName, items);
            database.KeyDelete(assistant.ProcessingQueueName);
        }
    }
}
