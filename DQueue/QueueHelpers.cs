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
        private static RabbitMQ.Client.ConnectionFactory _rabbitMQConnectionFactory;

        public static IQueueProvider CreateProvider(QueueProvider provider)
        {
            var appSettings = ConfigurationManager.AppSettings;

            if (provider == QueueProvider.Configured)
            {
                QueueProvider outProvider;
                var strProvider = appSettings["QueueProvider"];
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
                var hostName = appSettings["Redis_HostName"];
                var userName = appSettings["Redis_UserName"];
                var password = appSettings["Redis_Password"];
                return new RedisProvider(hostName, userName, password);
            }

            if (provider == QueueProvider.RabbitMQ)
            {
                if (_rabbitMQConnectionFactory == null)
                {
                    _rabbitMQConnectionFactory = new RabbitMQ.Client.ConnectionFactory
                    {
                        HostName = appSettings["RabbitMQ_HostName"],
                        UserName = appSettings["RabbitMQ_UserName"],
                        Password = appSettings["RabbitMQ_Password"]
                    };
                }

                return new RabbitMQProvider(_rabbitMQConnectionFactory);
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
