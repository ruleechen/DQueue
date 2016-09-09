using DQueue.Helpers;
using DQueue.Interfaces;
using RabbitMQ.Client;
using System;
using System.Text;

namespace DQueue.QueueProviders
{
    public class RabbitMQProvider : IQueueProvider
    {
        static Lazy<ConnectionFactory> _rabbitMQConnectionFactory = new Lazy<ConnectionFactory>(() =>
        {
            var rabbitMQConnectionString = ConfigSource.GetConnection("RabbitMQ_Connection");
            var rabbitMQConfiguration = RabbitMQConnectionConfiguration.Parse(rabbitMQConnectionString);
            return new ConnectionFactory
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

        public bool IgnoreHash { get; set; }

        public bool ExistsMessage(string queueName, object message)
        {
            throw new NotImplementedException();
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

                    var json = message.Serialize().AddEnqueueTime();
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

                    var receptionStatus = ReceptionStatus.Completed;

                    assistant.Cancellation.Register(() =>
                    {
                        receptionStatus = ReceptionStatus.Withdraw;
                        model.BasicCancel(consumer.ConsumerTag);
                    });

                    while (true)
                    {
                        if (receptionStatus == ReceptionStatus.Withdraw)
                        {
                            break;
                        }

                        var eventArg = consumer.Queue.Dequeue();

                        if (receptionStatus == ReceptionStatus.Withdraw)
                        {
                            break;
                        }

                        var message = default(TMessage);

                        if (eventArg != null)
                        {
                            var json = Encoding.UTF8.GetString(eventArg.Body);
                            message = json.Deserialize<TMessage>();
                        }

                        if (message != null)
                        {
                            handler(new ReceptionContext<TMessage>(message, (sender, status) =>
                            {
                                if (status == ReceptionStatus.Completed)
                                {
                                    model.BasicAck(eventArg.DeliveryTag, false);
                                }
                                else if (status == ReceptionStatus.Retry)
                                {
                                    throw new NotImplementedException();
                                }
                            }));
                        }
                    }
                }
            }
        }

    }
}
