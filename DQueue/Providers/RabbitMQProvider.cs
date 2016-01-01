using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DQueue.Core;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DQueue.Providers
{
    public class RabbitMQProvider : IQueue
    {
        static readonly ConnectionFactory _connectionFactory;

        static RabbitMQProvider()
        {
            _connectionFactory = new ConnectionFactory();
            _connectionFactory.HostName = "localhost";
            _connectionFactory.UserName = "rulee";
            _connectionFactory.Password = "abc123";
        }

        public void Send<T>(T message)
        {
            var queueName = GetQueueName<T>();
            var messageData = JsonConvert.SerializeObject(message);

            using (var connection = _connectionFactory.CreateConnection())
            {
                using (var channel = connection.CreateModel())
                {
                    channel.QueueDeclare(queueName, true, false, false, null);
                    var properties = channel.CreateBasicProperties();
                    properties.DeliveryMode = 2;
                    channel.BasicPublish("", "hello", properties, Encoding.UTF8.GetBytes(messageData));
                }
            }
        }

        public T Receive<T>()
        {
            var queueName = GetQueueName<T>();

            using (var connection = _connectionFactory.CreateConnection())
            {
                using (var channel = connection.CreateModel())
                {
                    channel.QueueDeclare(queueName, true, false, false, null);
                    channel.BasicQos(0, 1, false);

                    var consumer = new QueueingBasicConsumer(channel);
                    channel.BasicConsume("hello", false, consumer);

                    var eventArg = consumer.Queue.Dequeue();
                    var message = Encoding.UTF8.GetString(eventArg.Body);
                    channel.BasicAck(eventArg.DeliveryTag, false);

                    return JsonConvert.DeserializeObject<T>(message);
                }
            }
        }

        private static string GetQueueName<T>()
        {
            var type = typeof(T);

            if (typeof(IMessage).IsAssignableFrom(type))
            {
                try
                {
                    var instance = (IMessage)Activator.CreateInstance(type);
                    return instance.Queue;
                }
                catch (Exception)
                {
                }
            }

            return type.FullName;
        }
    }
}
