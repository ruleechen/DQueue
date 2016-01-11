using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using DQueue.Interfaces;
using DQueue.QueueProviders;

namespace DQueue
{
    internal class QueueHelpers
    {
        static Lazy<RabbitMQ.Client.ConnectionFactory> _rabbitMQConnectionFactory = new Lazy<RabbitMQ.Client.ConnectionFactory>(() =>
        {
            var appSettings = ConfigurationManager.AppSettings;
            return new RabbitMQ.Client.ConnectionFactory
            {
                HostName = appSettings["RabbitMQ_HostName"],
                UserName = appSettings["RabbitMQ_UserName"],
                Password = appSettings["RabbitMQ_Password"]
            };
        }, true);

        static Lazy<StackExchange.Redis.ConnectionMultiplexer> _redisConnectionFactory = new Lazy<StackExchange.Redis.ConnectionMultiplexer>(() =>
        {
            var redisConnectionString = ConfigurationManager.AppSettings["Redis_Connection"];
            var resisConfiguration = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
            return StackExchange.Redis.ConnectionMultiplexer.Connect(resisConfiguration);
        }, true);

        public static IQueueProvider CreateProvider(QueueProvider provider)
        {
            if (provider == QueueProvider.Configured)
            {
                QueueProvider outProvider;
                var strProvider = ConfigurationManager.AppSettings["QueueProvider"];
                if (Enum.TryParse<QueueProvider>(strProvider, true, out outProvider))
                {
                    provider = outProvider;
                }
                else
                {
                    throw new ArgumentException("Can not support queue provider: " + strProvider);
                }
            }

            if (provider == QueueProvider.Redis)
            {
                return new RedisProvider(_redisConnectionFactory.Value);
            }

            if (provider == QueueProvider.RabbitMQ)
            {
                return new RabbitMQProvider(_rabbitMQConnectionFactory.Value);
            }

            if (provider == QueueProvider.AspNet)
            {
                return new AspNetProvider();
            }

            throw new ArgumentException("Can not support queue provider: " + provider.ToString());
        }

        public static string GetQueueName(Type messageType)
        {
            if (typeof(IQueueMessage).IsAssignableFrom(messageType))
            {
                try
                {
                    var instance = (IQueueMessage)Activator.CreateInstance(messageType);
                    return instance.QueueName;
                }
                catch (Exception)
                {
                }
            }

            return messageType.FullName;
        }

        public static string GetQueueName<TMessage>()
            where TMessage : new()
        {
            var obj = new TMessage();

            var imsg = obj as IQueueMessage;

            if (imsg != null)
            {
                return imsg.QueueName;
            }
            else
            {
                return obj.GetType().FullName;
            }
        }
    }
}
