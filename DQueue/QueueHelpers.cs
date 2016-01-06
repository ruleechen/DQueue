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
        public static IQueueProvider GetProvider(QueueProvider provider)
        {
            var appSettings = ConfigurationManager.AppSettings;

            if (provider == QueueProvider.Configured)
            {
                var providerStr = appSettings["QueueProvider"];
                Enum.TryParse<QueueProvider>(providerStr, true, out provider);
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
                var hostName = appSettings["RabbitMQ_HostName"];
                var userName = appSettings["RabbitMQ_UserName"];
                var password = appSettings["RabbitMQ_Password"];
                return new RabbitMQProvider(hostName, userName, password);
            }

            if (provider == QueueProvider.AspNet)
            {
                return new AspNetProvider();
            }

            throw new ArgumentException("Can not support queue provider " + provider.ToString());
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
