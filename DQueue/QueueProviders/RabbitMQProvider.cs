using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DQueue.Interfaces;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DQueue.QueueProviders
{
    public class RabbitMQProvider : IQueueProvider
    {
        private readonly ConnectionFactory _connectionFactory;

        public RabbitMQProvider(string hostName, string userName, string password)
        {
            _connectionFactory = new ConnectionFactory
            {
                HostName = hostName,
                UserName = userName,
                Password = password
            };
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

        public void Dequeue<TMessage>(string queueName, Action<TMessage, ReceptionContext> handler)
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
                    model.BasicConsume(queueName, true, consumer);

                    ReceptionStatus = ReceptionStatus.Listen;

                    while (true)
                    {
                        if (ReceptionStatus == ReceptionStatus.BreakOff)
                        {
                            break;
                        }

                        if (ReceptionStatus == ReceptionStatus.Listen &&
                            ReceptionStatus != ReceptionStatus.Suspend)
                        {
                            var eventArg = consumer.Queue.DequeueNoWait(null);
                            if (eventArg != null)
                            {
                                var json = Encoding.UTF8.GetString(eventArg.Body);
                                var message = JsonConvert.DeserializeObject<TMessage>(json);

                                ReceptionStatus = ReceptionStatus.Process;

                                var context = new ReceptionContext(this);

                                handler(message, context);
                            }
                        }

                        System.Threading.Thread.Sleep(10);
                    }
                }
            }
        }

        public ReceptionStatus ReceptionStatus
        {
            get;
            set;
        }

        public void RequestStop()
        {
            ReceptionStatus = ReceptionStatus.BreakOff;
            System.Threading.Thread.Sleep(20);
        }
    }
}
