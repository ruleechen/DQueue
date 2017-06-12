using DQueue.Helpers;
using DQueue.Infrastructure;
using DQueue.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
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
        }, false);

        private readonly ConnectionFactory _connectionFactory;

        public RabbitMQProvider()
            : this(_rabbitMQConnectionFactory.Value)
        {
        }

        public RabbitMQProvider(ConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public bool ExistsMessage(string queueName, object message)
        {
            throw new NotImplementedException();
        }

        public void Enqueue(string queueName, object message, bool insertHash)
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

        public void Dequeue<TMessage>(ReceptionAssistant<TMessage> assistant, Action<ReceptionContext<TMessage>> handler)
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

                    assistant.Disposing += (s, e) =>
                    {
                        model.BasicCancel(consumer.ConsumerTag);
                    };

                    while (!assistant.IsTerminated())
                    {
                        BasicDeliverEventArgs eventArg = null;

                        var message = default(TMessage);

                        try
                        {
                            eventArg = consumer.Queue.Dequeue();

                            if (eventArg != null)
                            {
                                var json = Encoding.UTF8.GetString(eventArg.Body);
                                message = json.Deserialize<TMessage>();
                            }
                        }
                        catch (Exception ex)
                        {
                            LogFactory.GetLogger().Error("[RabbitMQProvider] Get Message Error!", ex);
                        }

                        if (message != null)
                        {
                            handler(new ReceptionContext<TMessage>(message, null, false, assistant, (sender, status) =>
                            {
                                if (status == DispatchStatus.Complete)
                                {
                                    model.BasicAck(eventArg.DeliveryTag, false);
                                }
                                else if (status == DispatchStatus.Timeout)
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
