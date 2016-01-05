using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DQueue.Interfaces;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DQueue.QueueProviders
{
    public class RabbitMQProvider : IQueueProvider
    {
        public void Send(string queueName, object message)
        {
            if (string.IsNullOrWhiteSpace(queueName) || message == null)
            {
                return;
            }

            var _connectionFactory = new ConnectionFactory();
            _connectionFactory.HostName = "localhost";
            _connectionFactory.UserName = "rulee";
            _connectionFactory.Password = "abc123";

            using (var connection = _connectionFactory.CreateConnection())
            {
                using (var channel = connection.CreateModel())
                {
                    channel.QueueDeclare(queueName, false, false, false, null);
                    var properties = channel.CreateBasicProperties();
                    properties.DeliveryMode = 2;

                    var messageData = JsonConvert.SerializeObject(message);
                    channel.BasicPublish("", "hello", properties, Encoding.UTF8.GetBytes(messageData));
                }
            }
        }

        public void Receive<TMessage>(string queueName, Action<TMessage, ReceptionContext> handler)
        {
            if (string.IsNullOrWhiteSpace(queueName) || handler == null)
            {
                return;
            }

            var _connectionFactory = new ConnectionFactory();
            _connectionFactory.HostName = "localhost";
            _connectionFactory.UserName = "rulee";
            _connectionFactory.Password = "abc123";

            using (var connection = _connectionFactory.CreateConnection())
            {
                using (var channel = connection.CreateModel())
                {
                    channel.QueueDeclare(queueName, false, false, false, null);

                    var consumer = new QueueingBasicConsumer(channel);
                    channel.BasicConsume(queueName, true, consumer);

                    var eventArg = consumer.Queue.Dequeue();
                    var message = Encoding.UTF8.GetString(eventArg.Body);

                    //return JsonConvert.DeserializeObject<TMessage>(message);
                }
            }
        }
    }
}
