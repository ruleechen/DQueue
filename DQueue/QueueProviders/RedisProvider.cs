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

            var subscriber = _connectionFactory.GetSubscriber();
            var database = _connectionFactory.GetDatabase();

            var json = JsonConvert.SerializeObject(message);
            database.ListLeftPush(queueName, json);
            subscriber.Publish(queueName, 1);
        }

        public void Dequeue<TMessage>(ReceptionManager manager, Action<ReceptionContext<TMessage>> handler)
        {
            if (manager == null || string.IsNullOrWhiteSpace(manager.QueueName) || handler == null)
            {
                return;
            }

            var subscriber = _connectionFactory.GetSubscriber();
            var database = _connectionFactory.GetDatabase();

            var queueLocker = manager.QueueLocker();
            var receptionLocker = new object();
            var receptionStatus = ReceptionStatus.Listen;

            manager.OnFallback(() =>
            {
                var items = database.ListRange(manager.ProcessingQueueName);
                database.ListRightPush(manager.QueueName, items);
                database.KeyDelete(manager.ProcessingQueueName);
            });

            manager.OnCancel(1, false, () =>
            {
                lock (receptionLocker)
                {
                    receptionStatus = ReceptionStatus.Withdraw;
                }
            });

            manager.OnCancel(2, true, () =>
            {
                lock (queueLocker)
                {
                    Monitor.PulseAll(queueLocker);
                }
            });

            subscriber.Subscribe(manager.QueueName, (channel, val) =>
            {

            });

            while (true)
            {
                lock (queueLocker)
                {
                    if (database.ListLength(manager.QueueName) == 0)
                    {
                        Monitor.Wait(queueLocker);
                    }
                }

                if (receptionStatus == ReceptionStatus.Withdraw)
                {
                    break;
                }

                var item = RedisValue.Null;
                object message = null;

                if (receptionStatus == ReceptionStatus.Listen)
                {
                    if (database.ListLength(manager.QueueName) > 0)
                    {
                        item = database.ListRightPopLeftPush(manager.QueueName, manager.ProcessingQueueName);
                        if (item != RedisValue.Null) { message = JsonConvert.DeserializeObject<TMessage>(item); }
                    }
                }

                if (message != null)
                {
                    var context = new ReceptionContext<TMessage>((TMessage)message, (sender, status) =>
                    {
                        if (status == ReceptionStatus.Success)
                        {
                            database.ListRemove(manager.ProcessingQueueName, item, 1);
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

                //Thread.Sleep(100);
            }
        }
    }
}
