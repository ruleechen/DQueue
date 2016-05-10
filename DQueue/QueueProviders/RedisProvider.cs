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
        static Lazy<StackExchange.Redis.ConnectionMultiplexer> _redisConnectionFactory = new Lazy<StackExchange.Redis.ConnectionMultiplexer>(() =>
        {
            var redisConnectionString = ConfigSource.Current.ConnectionStrings.ConnectionStrings["Redis_Connection"].ConnectionString;
            var resisConfiguration = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
            return StackExchange.Redis.ConnectionMultiplexer.Connect(resisConfiguration);
        }, true);

        private const string SubscriberKey = "$RedisQueueSubscriberKey$";
        private const string SubscriberValue = "$RedisQueueSubscriberValue$";
        private const string HashStorageKey = "$RedisHashStorageKey$";

        private readonly ConnectionMultiplexer _connectionFactory;

        public RedisProvider()
            : this(_redisConnectionFactory.Value)
        {
        }

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
            var hash = HashCodeGenerator.Calc(json);

            var database = _connectionFactory.GetDatabase();
            if (database.HashExists(queueName + HashStorageKey, hash))
            {
                return;
            }

            database.ListLeftPush(queueName, json);
            database.HashSet(queueName + HashStorageKey, hash, 1);

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

            var receptionLocker = new object();
            var receptionStatus = ReceptionStatus.Listen;

            assistant.RunForFirstThread(() =>
            {
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
            });

            assistant.RegisterCancel(0, true, () =>
            {
                subscriber.Unsubscribe(assistant.QueueName + SubscriberKey);
            });

            assistant.RegisterCancel(1, false, () =>
            {
                lock (receptionLocker)
                {
                    receptionStatus = ReceptionStatus.Withdraw;
                }
            });

            assistant.RegisterCancel(2, true, () =>
            {
                lock (receptionLocker)
                {
                    Monitor.PulseAll(receptionLocker);
                }

                lock (assistant.MonitorLocker)
                {
                    Monitor.PulseAll(assistant.MonitorLocker);
                }
            });

            assistant.RegisterFallback(() =>
            {
                var items = database.ListRange(assistant.ProcessingQueueName);
                database.ListRightPush(assistant.QueueName, items);
                database.KeyDelete(assistant.ProcessingQueueName);
            });

            while (true)
            {
                lock (receptionLocker)
                {
                    if (receptionStatus == ReceptionStatus.Process)
                    {
                        Monitor.Wait(receptionLocker);
                    }
                }

                if (receptionStatus == ReceptionStatus.Withdraw)
                {
                    break;
                }

                object message = null;
                var item = RedisValue.Null;

                lock (assistant.MonitorLocker)
                {
                    if (database.ListLength(assistant.QueueName) == 0)
                    {
                        Monitor.Wait(assistant.MonitorLocker);
                    }

                    if (receptionStatus == ReceptionStatus.Listen)
                    {
                        item = database.ListRightPopLeftPush(assistant.QueueName, assistant.ProcessingQueueName);
                        if (item != RedisValue.Null) { message = JsonConvert.DeserializeObject<TMessage>(item); }
                    }
                }

                if (receptionStatus == ReceptionStatus.Withdraw)
                {
                    break;
                }

                if (message != null)
                {
                    var context = new ReceptionContext<TMessage>((TMessage)message, (sender, status) =>
                    {
                        if (status == ReceptionStatus.Success)
                        {
                            database.ListRemove(assistant.ProcessingQueueName, item, 1);
                            database.HashDelete(assistant.QueueName + HashStorageKey, HashCodeGenerator.Calc(item));
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

                        lock (receptionLocker)
                        {
                            Monitor.Pulse(receptionLocker);
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

                //Thread.Sleep(100);
            }
        }

    }
}
