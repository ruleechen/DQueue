using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using DQueue.Helpers;
using DQueue.Interfaces;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DQueue.QueueProviders
{
    public class RabbitMQProvider : IQueueProvider
    {
        static Lazy<RabbitMQ.Client.ConnectionFactory> _rabbitMQConnectionFactory = new Lazy<RabbitMQ.Client.ConnectionFactory>(() =>
        {
            var rabbitMQConnectionString = ConfigSource.Current.ConnectionStrings.ConnectionStrings["RabbitMQ_Connection"].ConnectionString;
            var rabbitMQConfiguration = RabbitMQConnectionConfiguration.Parse(rabbitMQConnectionString);
            return new RabbitMQ.Client.ConnectionFactory
            {
                HostName = rabbitMQConfiguration.HostName,
                Port = rabbitMQConfiguration.Port,
                VirtualHost = rabbitMQConfiguration.VirtualHost,
                UserName = rabbitMQConfiguration.UserName,
                Password = rabbitMQConfiguration.Password,
                RequestedHeartbeat = rabbitMQConfiguration.RequestedHeartbeat,
                ClientProperties = rabbitMQConfiguration.ClientProperties
            };
        }, true);

        private readonly ConnectionFactory _connectionFactory;

        public RabbitMQProvider()
            : this(_rabbitMQConnectionFactory.Value)
        {
        }

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

        public void Dequeue<TMessage>(ReceptionAssistant assistant, Action<ReceptionContext<TMessage>> handler)
        {
            if (assistant == null || string.IsNullOrWhiteSpace(assistant.QueueName) || handler == null)
            {
                return;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                using (var model = connection.CreateModel())
                {
                    model.QueueDeclare(assistant.QueueName, false, false, false, null);
                    var consumer = new QueueingBasicConsumer(model);
                    model.BasicConsume(assistant.QueueName, false, consumer);

                    var receptionLocker = new object();
                    var receptionStatus = ReceptionStatus.Listen;

                    assistant.RegisterCancel(1, false, () =>
                    {
                        lock (receptionLocker)
                        {
                            receptionStatus = ReceptionStatus.Withdraw;
                        }
                    });

                    assistant.RegisterCancel(2, false, () =>
                    {
                        model.BasicCancel(consumer.ConsumerTag);
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

                        var eventArg = consumer.Queue.Dequeue();

                        if (receptionStatus == ReceptionStatus.Withdraw)
                        {
                            break;
                        }

                        if (receptionStatus == ReceptionStatus.Listen)
                        {
                            if (eventArg != null)
                            {
                                var json = Encoding.UTF8.GetString(eventArg.Body);
                                message = JsonConvert.DeserializeObject<TMessage>(json);
                            }
                        }

                        if (message != null)
                        {
                            var context = new ReceptionContext<TMessage>((TMessage)message, (sender, status) =>
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
    }
}
