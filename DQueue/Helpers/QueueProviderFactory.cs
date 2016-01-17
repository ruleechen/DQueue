using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using DQueue.Helpers;
using DQueue.Interfaces;
using DQueue.QueueProviders;

namespace DQueue.Helpers
{
    public class QueueProviderFactory
    {
        static Lazy<RabbitMQ.Client.ConnectionFactory> _rabbitMQConnectionFactory = new Lazy<RabbitMQ.Client.ConnectionFactory>(() =>
        {
            var rabbitMQConnectionString = ConfigurationManager.ConnectionStrings["RabbitMQ_Connection"].ConnectionString;
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

        static Lazy<StackExchange.Redis.ConnectionMultiplexer> _redisConnectionFactory = new Lazy<StackExchange.Redis.ConnectionMultiplexer>(() =>
        {
            var redisConnectionString = ConfigurationManager.ConnectionStrings["Redis_Connection"].ConnectionString;
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
    }
}
