using DQueue.Helpers;
using DQueue.Infrastructure;
using DQueue.Interfaces;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private const long DefaultHashValue = 1;

        private ConnectionMultiplexer _connectionFactory;

        public RedisProvider()
        {
            _connectionFactory = _redisConnectionFactory;
        }

        public bool ExistsMessage(string queueName, object message)
        {
            if (string.IsNullOrWhiteSpace(queueName) || message == null)
            {
                return false;
            }

            var database = _connectionFactory.GetDatabase();
            return ExistsMessage(database, queueName, message.Serialize());
        }

        private static bool ExistsMessage(IDatabase database, string queueName, string jsonMessage)
        {
            var hashKey = jsonMessage.GetMD5();
            var hashValue = database.HashGet(queueName + HashQueuePostfix, hashKey);

            if (hashValue != RedisValue.Null)
            {
                var messagePending = (hashValue == DefaultHashValue);
                if (messagePending)
                {
                    return true;
                }

                var messageProcessing = false; long utcTicks;
                if (long.TryParse(hashValue.GetString(), out utcTicks))
                {
                    var consumerTimeout = (DQueueSettings.Get().ConsumerTimeout ?? Constants.DefaultTimeoutTimeSpan.AsNullableTimeSpan().Value);
                    messageProcessing = (DateTime.UtcNow.Subtract(new DateTime(utcTicks, DateTimeKind.Utc)) <= consumerTimeout);
                }
                if (messageProcessing)
                {
                    return true;
                }
            }

            return false;
        }

        public void Enqueue(string queueName, object message, bool insertHash)
        {
            if (string.IsNullOrWhiteSpace(queueName) || message == null)
            {
                return;
            }

            var database = _connectionFactory.GetDatabase();
            var subscriber = _connectionFactory.GetSubscriber();

            Enqueue(database, subscriber, queueName, new MessageModel
            {
                JsonMessage = message.Serialize(),
                InsertHash = insertHash,
            });
        }

        private class MessageModel
        {
            public string JsonMessage { get; set; }
            public bool InsertHash { get; set; }
        }

        private static void Enqueue(IDatabase database, ISubscriber subscriber, string queueName, params MessageModel[] messageModels)
        {
            messageModels = messageModels.Where(x => !x.InsertHash || !ExistsMessage(database, queueName, x.JsonMessage)).ToArray();

            var messages = new List<RedisValue>();

            var hashEntities = new List<HashEntry>();

            foreach (var item in messageModels)
            {
                messages.Add(item.JsonMessage.AddEnqueueTime());

                if (item.InsertHash)
                {
                    hashEntities.Add(new HashEntry(item.JsonMessage.GetMD5(), DefaultHashValue));
                }
            }

            database.ListLeftPush(queueName, messages.ToArray());

            if (hashEntities.Any())
            {
                database.HashSet(queueName + HashQueuePostfix, hashEntities.ToArray());
            }

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

            RequeueProcessingMessages(_connectionFactory, assistant);

            assistant.Disposing += (s, e) =>
            {
                _connectionFactory.GetSubscriber().Unsubscribe(assistant.QueueName + SubscriberKey);

                lock (assistant.DequeueLocker)
                {
                    Monitor.PulseAll(assistant.DequeueLocker);
                }

                RequeueProcessingMessages(_connectionFactory, assistant);
            };

            while (!assistant.IsTerminated())
            {
                var message = default(TMessage);
                var rawMessage = RedisValue.Null;
                var hashExists = false;

                lock (assistant.DequeueLocker)
                {
                    var database = _connectionFactory.GetDatabase();
                    if (database.ListLength(assistant.QueueName) == 0)
                    {
                        Monitor.Wait(assistant.DequeueLocker);
                    }

                    rawMessage = database.ListRightPopLeftPush(assistant.QueueName, assistant.ProcessingQueueName);

                    var jsonMessage = rawMessage.GetString();

                    var hashKey = jsonMessage.RemoveEnqueueTime().GetMD5();

                    hashExists = database.HashExists(assistant.QueueName + HashQueuePostfix, hashKey);

                    if (hashExists)
                    {
                        var hashValue = DateTime.UtcNow.Ticks; // utc now for different timezone clients
                        database.HashSet(assistant.QueueName + HashQueuePostfix, hashKey, hashValue);
                    }

                    try
                    {
                        message = jsonMessage.Deserialize<TMessage>();
                    }
                    catch (Exception ex)
                    {
                        RemoveProcessingMessage(database, assistant, rawMessage);
                        LogFactory.GetLogger().Error(string.Format("[RedisProvider] Deserialize failed on raw message: \"{0}\".", rawMessage), ex);
                    }
                }

                if (message != null)
                {
                    var feedbackHandler = GetFeedbackHandler<TMessage>(_connectionFactory);
                    handler(new ReceptionContext<TMessage>(message, rawMessage, hashExists, assistant, feedbackHandler));
                }
            }
        }

        private static void RemoveProcessingMessage<TMessage>(IDatabase database, ReceptionAssistant<TMessage> assistant, RedisValue rawMessage)
        {
            if (rawMessage == RedisValue.Null)
            {
                return;
            }

            try
            {
                var hashKey = rawMessage.GetString().RemoveEnqueueTime().GetMD5();
                database.HashDelete(assistant.QueueName + HashQueuePostfix, hashKey);
                database.ListRemove(assistant.ProcessingQueueName, rawMessage, 1);
            }
            catch { }
        }

        private static Action<ReceptionContext<TMessage>, DispatchStatus>
            GetFeedbackHandler<TMessage>(ConnectionMultiplexer connection)
        {
            // create new action for each ReceptionContext
            // for avoid feedback error when RedisProvider has disposed
            return (context, status) =>
            {
                var assistant = context.Assistant;
                var rawMessage = (RedisValue)context.RawMessage;
                var database = connection.GetDatabase();

                if (status == DispatchStatus.Complete)
                {
                    RemoveProcessingMessage(database, assistant, rawMessage);
                }
                else if (status == DispatchStatus.Timeout)
                {
                    database.ListRemove(assistant.ProcessingQueueName, rawMessage, 1);
                    // requeue current message for a new try when timeout
                    var jsonMessage = rawMessage.GetString().RemoveEnqueueTime();
                    var subscriber = connection.GetSubscriber();
                    Enqueue(database, subscriber, context.Assistant.QueueName, new MessageModel
                    {
                        JsonMessage = jsonMessage,
                        InsertHash = context.HashExists,
                    });
                }
            };
        }

        private static void RequeueProcessingMessages<TMessage>(ConnectionMultiplexer connection, ReceptionAssistant<TMessage> assistant)
        {
            var database = connection.GetDatabase();
            var messages = database.ListRange(assistant.ProcessingQueueName);
            if (messages.Any())
            {
                var messageModels = messages.Select(x =>
                {
                    var jsonMessage = x.GetString().RemoveEnqueueTime();
                    var hashKey = jsonMessage.GetMD5();
                    var hashExists = database.HashExists(assistant.QueueName + HashQueuePostfix, hashKey);
                    return new MessageModel
                    {
                        JsonMessage = jsonMessage,
                        InsertHash = hashExists,
                    };
                });

                var subscriber = connection.GetSubscriber();
                Enqueue(database, subscriber, assistant.QueueName, messageModels.ToArray());
                database.KeyDelete(assistant.ProcessingQueueName);
            }
        }
    }
}
