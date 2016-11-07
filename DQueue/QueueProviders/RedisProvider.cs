using DQueue.Infrastructure;
using DQueue.Interfaces;
using StackExchange.Redis;
using System;
using System.Threading;

namespace DQueue.QueueProviders
{
    public class RedisProvider : IQueueProvider
    {
        //https://github.com/StackExchange/StackExchange.Redis/blob/master/Docs/Basics.md
        //Because the ConnectionMultiplexer does a lot, it is designed to be shared and reused between callers.
        //You should not create a ConnectionMultiplexer per operation.
        //It is fully thread-safe and ready for this usage.
        static ConnectionMultiplexer _redisConnectionFactory;

        static RedisProvider()
        {
            var redisConnectionString = ConfigSource.GetConnection("Redis_Connection");
            var resisConfiguration = ConfigurationOptions.Parse(redisConnectionString);
            _redisConnectionFactory = ConnectionMultiplexer.Connect(resisConfiguration);
        }

        private const string SubscriberKey = "-$SubscriberKey$";
        private const string SubscriberValue = "-$SubscriberValue$";
        private const string HashQueuePostfix = "-$Hash$";

        private ConnectionMultiplexer _connectionFactory;

        public RedisProvider()
        {
            _connectionFactory = _redisConnectionFactory;
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
            return database.HashExists(queueName + HashQueuePostfix, hash);
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
                if (database.HashExists(queueName + HashQueuePostfix, hash))
                {
                    return;
                }
            }

            database.ListLeftPush(queueName, json.AddEnqueueTime());

            if (!IgnoreHash)
            {
                database.HashSet(queueName + HashQueuePostfix, hash, 1);
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

            _connectionFactory.GetSubscriber().Subscribe(assistant.QueueName + SubscriberKey, (channel, val) =>
            {
                if (val == SubscriberValue)
                {
                    lock (assistant.DequeueLocker)
                    {
                        Monitor.Pulse(assistant.DequeueLocker);
                    }
                }
            });

            RequeueProcessingMessages(assistant);

            assistant.Cancellation.Register(() =>
            {
                _connectionFactory.GetSubscriber().Unsubscribe(assistant.QueueName + SubscriberKey);

                lock (assistant.DequeueLocker)
                {
                    Monitor.PulseAll(assistant.DequeueLocker);
                }

                RequeueProcessingMessages(assistant);
            });

            while (!assistant.IsTerminated())
            {
                var message = default(TMessage);
                var rawMessage = RedisValue.Null;

                lock (assistant.DequeueLocker)
                {
                    var database = _connectionFactory.GetDatabase();
                    if (database.ListLength(assistant.QueueName) == 0)
                    {
                        Monitor.Wait(assistant.DequeueLocker);
                    }

                    try
                    {
                        rawMessage = database.ListRightPopLeftPush(assistant.QueueName, assistant.ProcessingQueueName);
                        message = rawMessage.GetString().Deserialize<TMessage>();
                    }
                    catch (Exception ex)
                    {
                        LogFactory.GetLogger().Error(string.Format("[RedisProvider] Get Message Error! Raw Message: \"{0}\".", rawMessage), ex);
                        RemoveProcessingMessage(assistant, database, rawMessage);
                    }
                }

                if (message != null)
                {
                    handler(new ReceptionContext<TMessage>(message, rawMessage, assistant, FeedbackHandler));
                }
            }
        }

        private void RemoveProcessingMessage<TMessage>(ReceptionAssistant<TMessage> assistant, IDatabase database, RedisValue rawMessage)
        {
            if (rawMessage == RedisValue.Null)
            {
                return;
            }

            try
            {
                database.ListRemove(assistant.ProcessingQueueName, rawMessage, 1);
                database.HashDelete(assistant.QueueName + HashQueuePostfix, rawMessage.GetString().RemoveEnqueueTime().GetMD5());
            }
            catch { }
        }

        private void FeedbackHandler<TMessage>(ReceptionContext<TMessage> context, DispatchStatus status)
        {
            var assistant = context.Assistant;
            var rawMessage = (RedisValue)context.RawMessage;
            var database = _connectionFactory.GetDatabase();

            if (status == DispatchStatus.Complete)
            {
                RemoveProcessingMessage(assistant, database, rawMessage);
            }
            else if (status == DispatchStatus.Timeout)
            {
                database.ListRemove(assistant.ProcessingQueueName, rawMessage, 1);
                database.ListLeftPush(assistant.QueueName, rawMessage.GetString().RemoveEnqueueTime().AddEnqueueTime());
            }
        }

        private void RequeueProcessingMessages<TMessage>(ReceptionAssistant<TMessage> assistant)
        {
            var database = _connectionFactory.GetDatabase();
            var items = database.ListRange(assistant.ProcessingQueueName);
            database.ListRightPush(assistant.QueueName, items);
            database.KeyDelete(assistant.ProcessingQueueName);
        }
    }
}
