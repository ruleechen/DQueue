using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using DQueue.Interfaces;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DQueue.QueueProviders
{
    public class RabbitMQProvider : IQueueProvider
    {
        private static IConnection GetConnection()
        {
            var connectionFactory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "rulee",
                Password = "abc123"
            };

            return connectionFactory.CreateConnection();
        }

        public void Send(string queueName, object message)
        {
            if (string.IsNullOrWhiteSpace(queueName) || message == null)
            {
                return;
            }

            using (var connection = GetConnection())
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

        public void Receive<TMessage>(string queueName, Action<TMessage, ReceptionContext> handler)
        {
            if (string.IsNullOrWhiteSpace(queueName) || handler == null)
            {
                return;
            }

            using (var connection = GetConnection())
            {
                using (var model = connection.CreateModel())
                {
                    model.QueueDeclare(queueName, false, false, false, null);
                    var consumer = new QueueingBasicConsumer(model);
                    model.BasicConsume(queueName, true, consumer);

                    var receptionStatus = ReceptionStatus.Querying;

                    while (true)
                    {
                        if (receptionStatus == ReceptionStatus.BreakOff)
                        {
                            break;
                        }

                        if (receptionStatus == ReceptionStatus.Querying)
                        {
                            var eventArg = consumer.Queue.Dequeue();
                            var json = Encoding.UTF8.GetString(eventArg.Body);
                            var message = JsonConvert.DeserializeObject<TMessage>(json);

                            receptionStatus = ReceptionStatus.Processing;

                            var context = new ReceptionContext((status) =>
                            {
                                receptionStatus = status;
                            });

                            handler(message, context);
                        }
                    }
                }
            }
        }
    }
}
