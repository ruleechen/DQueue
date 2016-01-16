using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using DQueue.Interfaces;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DQueue.QueueProviders
{
    public class RabbitMQProvider : IQueueProvider
    {
        private readonly ConnectionFactory _connectionFactory;

        public RabbitMQProvider(ConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public void Enqueue(string queueName, object message)
        {
            if (string.IsNullOrWhiteSpace(queueName) || message == null)
            {
                return;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                using (var model = connection.CreateModel())
                {
                    model.QueueDeclare(queueName, false, false, false, null);
                    var basicProperties = model.CreateBasicProperties();
                    basicProperties.Persistent = true;

                    var json = JsonConvert.SerializeObject(message);
                    var body = Encoding.UTF8.GetBytes(json);

                    model.BasicPublish(string.Empty, queueName, basicProperties, body);
                }
            }
        }

        public void Dequeue<TMessage>(string queueName, Action<ReceptionContext<TMessage>> handler, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(queueName) || handler == null)
            {
                return;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                using (var model = connection.CreateModel())
                {
                    model.QueueDeclare(queueName, false, false, false, null);
                    var consumer = new QueueingBasicConsumer(model);
                    model.BasicConsume(queueName, false, consumer);

                    token.Register(() =>
                    {
                        model.BasicCancel(consumer.ConsumerTag);
                    });

                    var receptionLocker = new object();
                    var receptionStatus = ReceptionStatus.Listen;

                    token.Register(() =>
                    {
                        lock (receptionLocker)
                        {
                            receptionStatus = ReceptionStatus.Withdraw;
                        }
                    });

                    while (true)
                    {
                        if (receptionStatus == ReceptionStatus.Withdraw)
                        {
                            break;
                        }

                        if (receptionStatus == ReceptionStatus.Listen)
                        {
                            var eventArg = consumer.Queue.Dequeue();
                            if (eventArg != null)
                            {
                                var json = Encoding.UTF8.GetString(eventArg.Body);
                                var message = JsonConvert.DeserializeObject<TMessage>(json);

                                var context = new ReceptionContext<TMessage>(message, (sender, status) =>
                                {
                                    if (status == ReceptionStatus.Success)
                                    {
                                        model.BasicAck(eventArg.DeliveryTag, false);
                                        status = ReceptionStatus.Listen;
                                    }

                                    if (receptionStatus != ReceptionStatus.Withdraw)
                                    {
                                        lock (receptionLocker)
                                        {
                                            receptionStatus = status;
                                        }
                                    }
                                });

                                lock (receptionLocker)
                                {
                                    receptionStatus = ReceptionStatus.Process;
                                }

                                handler(context);
                            }
                        }
                    }
                }
            }
        }
    }
}
